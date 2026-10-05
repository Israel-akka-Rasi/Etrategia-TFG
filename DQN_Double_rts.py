"""
DQN PARA EL RTS (TFG)
=====================
Entrena (o evalúa) un agente DQN sobre el MISMO ejecutable de Unity que PPO,
con las mismas observaciones (22), las mismas 20 acciones, la misma máscara
de acciones, la misma recompensa y el mismo número de pasos.

Por qué una implementación propia y no Stable-Baselines3:
  - Cada ejecutable tiene 4 agentes (uno por área) que piden decisión cada
    uno a su ritmo (1 s de juego). El adaptador de ML-Agents para Gym solo
    admite un agente, y el DQN de SB3 no admite máscara de acciones.
  - El algoritmo es el DQN estándar (Mnih et al., 2015): red Q, memoria de
    repetición, red objetivo y exploración epsilon-greedy. La única adición es
    la máscara: las acciones imposibles no se eligen ni cuentan en el máximo
    del objetivo, igual que PPO no puede elegirlas.

Uso (desde la carpeta del proyecto, con el entorno activado):
  Entrenar:  python dqn_rts.py --run-id dqn_v9_seed1 --seed 1
  Probar:    python dqn_rts.py --run-id dqn_prueba --pasos 3000 --num-envs 1 --force
  Evaluar:   python dqn_rts.py --run-id dqn_v9_seed1 --evaluar --partidas 250

Resultados en results/<run-id>/AgenteRTS/ (TensorBoard, con los mismos nombres
de métricas que PPO para poder superponer las curvas) y el modelo en
results/<run-id>/dqn_final.pt.
"""
import argparse
import os
import random
import shutil
import sys
import time
from collections import defaultdict
from concurrent.futures import ThreadPoolExecutor

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F
from torch.utils.tensorboard import SummaryWriter

from mlagents_envs.environment import UnityEnvironment, ActionTuple
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.stats_side_channel import StatsSideChannel


# ---------------------------------------------------------------------------
# Configuración
# ---------------------------------------------------------------------------
def leer_argumentos():
    p = argparse.ArgumentParser(description="DQN para el RTS del TFG")
    p.add_argument("--env", default="Builds/Entrenamiento/Estrategia-TFG")
    p.add_argument("--run-id", required=True)
    p.add_argument("--seed", type=int, default=1)
    p.add_argument("--num-envs", type=int, default=4, help="ejecutables en paralelo (4 agentes cada uno)")
    p.add_argument("--base-port", type=int, default=5005)
    p.add_argument("--time-scale", type=float, default=20.0)
    p.add_argument("--capture-frame-rate", type=int, default=300)
    p.add_argument("--force", action="store_true", help="borra results/<run-id> si ya existe")

    # Presupuesto y registro (iguales que PPO)
    p.add_argument("--pasos", type=int, default=500_000, help="decisiones totales de todos los agentes")
    p.add_argument("--summary-freq", type=int, default=10_000)
    p.add_argument("--guardar-cada", type=int, default=50_000)

    # Hiperparámetros de DQN
    p.add_argument("--gamma", type=float, default=0.998, help="igual que PPO")
    p.add_argument("--lr", type=float, default=1e-4)
    p.add_argument("--buffer", type=int, default=200_000)
    p.add_argument("--batch", type=int, default=64)
    p.add_argument("--inicio-aprendizaje", type=int, default=10_000)
    p.add_argument("--entrenar-cada", type=int, default=4, help="un paso de gradiente cada N decisiones")
    p.add_argument("--actualizar-objetivo", type=int, default=5_000, help="copiar la red objetivo cada N decisiones")
    p.add_argument("--eps-inicial", type=float, default=1.0)
    p.add_argument("--eps-final", type=float, default=0.05)
    p.add_argument("--fraccion-exploracion", type=float, default=0.25, help="fracción de pasos en que epsilon baja")
    p.add_argument("--hidden", type=int, default=128, help="igual que PPO: 2 capas de 128")

    # Evaluación
    p.add_argument("--evaluar", action="store_true")
    p.add_argument("--modelo", default=None, help="por defecto results/<run-id>/dqn_final.pt")
    p.add_argument("--partidas", type=int, default=250)
    p.add_argument("--eps-eval", type=float, default=0.0, help="0 = siempre la mejor acción")
    return p.parse_args()


# ---------------------------------------------------------------------------
# Red Q (misma arquitectura que la de PPO: 2 capas ocultas de 128)
# ---------------------------------------------------------------------------
class RedQ(nn.Module):
    def __init__(self, n_obs, n_acciones, hidden):
        super().__init__()
        self.red = nn.Sequential(
            nn.Linear(n_obs, hidden), nn.ReLU(),
            nn.Linear(hidden, hidden), nn.ReLU(),
            nn.Linear(hidden, n_acciones),
        )

    def forward(self, x):
        return self.red(x)


# ---------------------------------------------------------------------------
# Memoria de repetición
# ---------------------------------------------------------------------------
class Memoria:
    def __init__(self, capacidad, n_obs, n_acciones):
        self.cap, self.i, self.n = capacidad, 0, 0
        self.obs = np.zeros((capacidad, n_obs), np.float32)
        self.acc = np.zeros(capacidad, np.int64)
        self.rec = np.zeros(capacidad, np.float32)
        self.sig = np.zeros((capacidad, n_obs), np.float32)
        self.fin = np.zeros(capacidad, np.float32)
        self.mas = np.ones((capacidad, n_acciones), bool)  # acciones válidas en el estado siguiente

    def guardar(self, o, a, r, o2, fin, mascara2):
        self.obs[self.i], self.acc[self.i], self.rec[self.i] = o, a, r
        self.sig[self.i], self.fin[self.i], self.mas[self.i] = o2, fin, mascara2
        self.i = (self.i + 1) % self.cap
        self.n = min(self.n + 1, self.cap)

    def muestra(self, k):
        idx = np.random.randint(0, self.n, size=k)
        t = lambda x: torch.from_numpy(x[idx])
        return t(self.obs), t(self.acc), t(self.rec), t(self.sig), t(self.fin), t(self.mas)


# ---------------------------------------------------------------------------
# Entornos de Unity
# ---------------------------------------------------------------------------
def abrir_entornos(args):
    entornos, stats = [], []
    for w in range(args.num_envs):
        motor, est = EngineConfigurationChannel(), StatsSideChannel()
        env = UnityEnvironment(file_name=args.env, worker_id=w, base_port=args.base_port,
                               seed=args.seed * 100 + w, no_graphics=True,
                               side_channels=[motor, est], timeout_wait=180)
        motor.set_configuration_parameters(time_scale=args.time_scale,
                                           capture_frame_rate=args.capture_frame_rate)
        env.reset()
        entornos.append(env)
        stats.append(est)
        print(f"Entorno {w} conectado (puerto {args.base_port + w})", flush=True)
    nombre = list(entornos[0].behavior_specs.keys())[0]
    spec = entornos[0].behavior_specs[nombre]
    n_obs = spec.observation_specs[0].shape[0]
    n_acc = spec.action_spec.discrete_branches[0]
    print(f"Comportamiento '{nombre}': {n_obs} observaciones, {n_acc} acciones", flush=True)
    return entornos, stats, nombre, n_obs, n_acc


def mascara_valida(paso_decision, n_acc):
    """Devuelve True en las acciones POSIBLES (ML-Agents marca True las bloqueadas)."""
    if paso_decision.action_mask is None:
        return np.ones((len(paso_decision), n_acc), bool)
    return ~paso_decision.action_mask[0].astype(bool)


def elegir(red, obs, validas, eps):
    """epsilon-greedy restringido a las acciones válidas."""
    n = obs.shape[0]
    with torch.no_grad():
        q = red(torch.from_numpy(obs.astype(np.float32))).numpy()
    q[~validas] = -np.inf
    acciones = q.argmax(axis=1)
    for k in range(n):
        if random.random() < eps:
            acciones[k] = np.random.choice(np.flatnonzero(validas[k]))
    return acciones, q


# ---------------------------------------------------------------------------
# Bucle principal
# ---------------------------------------------------------------------------
def main():
    args = leer_argumentos()
    random.seed(args.seed)
    np.random.seed(args.seed)
    torch.manual_seed(args.seed)
    torch.set_num_threads(2)

    carpeta = os.path.join("results", args.run_id)
    if not args.evaluar:
        if os.path.exists(carpeta):
            if not args.force:
                sys.exit(f"Ya existe {carpeta}. Usa otro --run-id o añade --force.")
            shutil.rmtree(carpeta)
        os.makedirs(carpeta)

    entornos, canales_stats, nombre, n_obs, n_acc = abrir_entornos(args)
    pool = ThreadPoolExecutor(max_workers=len(entornos))

    red = RedQ(n_obs, n_acc, args.hidden)
    if args.evaluar:
        ruta = args.modelo or os.path.join(carpeta, "dqn_final.pt")
        red.load_state_dict(torch.load(ruta, map_location="cpu"))
        red.eval()
        print(f"Evaluando {ruta} durante {args.partidas} partidas (epsilon {args.eps_eval})", flush=True)
    else:
        objetivo = RedQ(n_obs, n_acc, args.hidden)
        objetivo.load_state_dict(red.state_dict())
        optim = torch.optim.Adam(red.parameters(), lr=args.lr)
        memoria = Memoria(args.buffer, n_obs, n_acc)
        writer = SummaryWriter(os.path.join(carpeta, "AgenteRTS"))
        with open(os.path.join(carpeta, "configuracion.txt"), "w", encoding="utf-8") as f:
            for k, v in sorted(vars(args).items()):
                f.write(f"{k}: {v}\n")

    # Estado pendiente por agente: (observación, acción) de su última decisión
    pendiente = {}                      # clave (entorno, agent_id)
    recompensa_ep = defaultdict(float)  # para Environment/Cumulative Reward
    pasos_ep = defaultdict(int)         # para Environment/Episode Length
    ep_recompensas, ep_longitudes, perdidas, q_medias = [], [], [], []
    stats_acumuladas = defaultdict(list)

    pasos, partidas, gradientes = 0, 0, 0
    siguiente_resumen = args.summary_freq
    siguiente_guardado = args.guardar_cada
    inicio = time.time()
    pasos_exploracion = max(1, int(args.fraccion_exploracion * args.pasos))

    def epsilon():
        if args.evaluar:
            return args.eps_eval
        f = min(1.0, pasos / pasos_exploracion)
        return args.eps_inicial + f * (args.eps_final - args.eps_inicial)

    def guardar(nombre_archivo):
        torch.save(red.state_dict(), os.path.join(carpeta, nombre_archivo))

    try:
        while True:
            if args.evaluar and partidas >= args.partidas:
                break
            if not args.evaluar and pasos >= args.pasos:
                break

            for e, env in enumerate(entornos):
                decision, terminal = env.get_steps(nombre)

                # 1) Agentes cuya partida ha terminado: transición final
                for k, aid in enumerate(terminal.agent_id):
                    clave = (e, int(aid))
                    r = float(terminal.reward[k])
                    recompensa_ep[clave] += r
                    if clave in pendiente:
                        o, a = pendiente.pop(clave)
                        fin = 0.0 if terminal.interrupted[k] else 1.0
                        if not args.evaluar:
                            memoria.guardar(o, a, r, terminal.obs[0][k], fin, np.ones(n_acc, bool))
                    ep_recompensas.append(recompensa_ep.pop(clave, 0.0))
                    ep_longitudes.append(pasos_ep.pop(clave, 0))
                    partidas += 1
                    if args.evaluar:
                        print(f"  partida {partidas}/{args.partidas} terminada", flush=True)

                if len(decision) == 0:
                    continue

                # 2) Agentes que piden decisión
                obs = decision.obs[0]
                validas = mascara_valida(decision, n_acc)
                for k, aid in enumerate(decision.agent_id):
                    clave = (e, int(aid))
                    r = float(decision.reward[k])
                    recompensa_ep[clave] += r
                    if clave in pendiente and not args.evaluar:
                        o, a = pendiente[clave]
                        memoria.guardar(o, a, r, obs[k], 0.0, validas[k])

                acciones, q = elegir(red, obs, validas, epsilon())
                for k, aid in enumerate(decision.agent_id):
                    clave = (e, int(aid))
                    pendiente[clave] = (obs[k].copy(), int(acciones[k]))
                    pasos_ep[clave] += 1
                env.set_actions(nombre, ActionTuple(discrete=acciones.reshape(-1, 1).astype(np.int32)))

                if args.evaluar:
                    continue

                # 3) Aprendizaje (por cada decisión tomada)
                for _ in range(len(acciones)):
                    pasos += 1
                    if memoria.n >= args.inicio_aprendizaje and pasos % args.entrenar_cada == 0:
                        o, a, r, o2, fin, m2 = memoria.muestra(args.batch)
                        with torch.no_grad():
                            q2 = objetivo(o2)
                            q2[~m2] = -1e9          # solo acciones válidas en el máximo
                            y = r + args.gamma * (1.0 - fin) * q2.max(dim=1).values
                        q_actual = red(o).gather(1, a.unsqueeze(1)).squeeze(1)
                        perdida = F.smooth_l1_loss(q_actual, y)
                        optim.zero_grad()
                        perdida.backward()
                        nn.utils.clip_grad_norm_(red.parameters(), 10.0)
                        optim.step()
                        gradientes += 1
                        perdidas.append(perdida.item())
                        q_medias.append(q_actual.mean().item())
                    if pasos % args.actualizar_objetivo == 0:
                        objetivo.load_state_dict(red.state_dict())

            # Avanzar los entornos en paralelo
            list(pool.map(lambda en: en.step(), entornos))

            # Métricas del juego (las mismas que publica AgenteRTS para PPO)
            for canal in canales_stats:
                for clave_s, valores in canal.get_and_reset_stats().items():
                    stats_acumuladas[clave_s].extend(v for v, _ in valores)

            if not args.evaluar and pasos >= siguiente_resumen:
                siguiente_resumen += args.summary_freq
                media = lambda x: float(np.mean(x)) if x else float("nan")
                writer.add_scalar("Environment/Cumulative Reward", media(ep_recompensas), pasos)
                writer.add_scalar("Environment/Episode Length", media(ep_longitudes), pasos)
                writer.add_scalar("Policy/Epsilon DQN", epsilon(), pasos)
                writer.add_scalar("Losses/Q Loss", media(perdidas), pasos)
                writer.add_scalar("Policy/Q Medio", media(q_medias), pasos)
                for clave_s, vals in stats_acumuladas.items():
                    if vals:
                        writer.add_scalar(clave_s, float(np.mean(vals)), pasos)
                writer.flush()
                vic = media(stats_acumuladas.get("Juego/TasaVictoria", []))
                print(f"Paso {pasos:7d} | eps {epsilon():.3f} | partidas {len(ep_recompensas):4d} | "
                      f"recompensa {media(ep_recompensas):6.3f} | victorias {vic:.0%} | "
                      f"pérdida {media(perdidas):.4f} | {time.time() - inicio:6.0f} s", flush=True)
                ep_recompensas, ep_longitudes, perdidas, q_medias = [], [], [], []
                stats_acumuladas.clear()

            if not args.evaluar and pasos >= siguiente_guardado:
                siguiente_guardado += args.guardar_cada
                guardar(f"dqn_{pasos}.pt")

    except KeyboardInterrupt:
        print("Interrumpido por el usuario.", flush=True)
    finally:
        if not args.evaluar:
            guardar("dqn_final.pt")
            writer.close()
            print(f"Modelo guardado en {os.path.join(carpeta, 'dqn_final.pt')}", flush=True)
        else:
            print(f"Evaluación terminada: {partidas} partidas. El resultado está en el log_partida del ejecutable.", flush=True)
        for env in entornos:
            env.close()
        pool.shutdown()


if __name__ == "__main__":
    main()