Нормализация OFI — критический этап, так как значения потока ордеров могут варьироваться от нуля до миллионов в зависимости от волатильности. Если подать нейросети «сырой» OFI, градиенты будут взрываться.
Для HFT и RL-агентов (Stable Baselines3) лучше всего подходят следующие три метода:
## 1. Robust Scaling (на базе квантилей)
В HFT данных очень много выбросов (крупные «прострелы» ликвидности). Обычный Z-score (среднее и стандартное отклонение) сильно искажается такими выбросами.

* Метод: Мы вычитаем медиану и делим на межквартильный размах (IQR).
* Почему это круто: Огромные одиночные сделки не «сжимают» остальные полезные данные в ноль.

# Реализация в Polarsdef robust_scale(col_name):
    return (pl.col(col_name) - pl.col(col_name).median()) / \           (pl.col(col_name).quantile(0.75) - pl.col(col_name).quantile(0.25))

## 2. Tanh или Softsign Transformation
Это нелинейное сжатие, которое превращает бесконечный диапазон OFI в диапазон $[-1, 1]$.

* Формула: $x_{scaled} = \tanh(x / \alpha)$, где $\alpha$ — коэффициент масштабирования.
* Зачем: Это сохраняет знак (покупка/продажа) и делает данные очень удобными для функций активации нейросетей (особенно если вы используете Tanh в самой политике PPO).

## 3. Rolling Z-Score (Стриминговая нормализация)
Поскольку рынок меняет режим (ночью объемы меньше, чем днем), статическая нормализация по всему дню может «ослепить» агента ночью.

* Метод: Рассчитывать среднее и отклонение в скользящем окне (например, за последние 1000–5000 тиков).
* Для RL: Это позволяет агенту понимать «сейчас OFI выше среднего относительно последнего часа».

------------------------------
## Практический пример пайплайна нормализации в Polars

# Оптимальная цепочка для подготовки OFI к RLprocessed_df = df.with_columns([
    # 1. Сначала логарифмируем объем, чтобы сгладить масштаб
    pl.col("ofi").sign() * (pl.col("ofi").abs() + 1).log().alias("ofi_log")
]).with_columns([
    # 2. Затем применяем Rolling Z-score для адаптации к волатильности
    ((pl.col("ofi_log") - pl.col("ofi_log").rolling_mean(window_size=5000)) / 
     (pl.col("ofi_log").rolling_std(window_size=5000) + 1e-6))
    .fill_null(0)
    .alias("ofi_final")
])

## Что выбрать для Stable Baselines3?
Если вы используете обертку VecNormalize в SB3, она делает Running Mean/Std автоматически. Однако до этого крайне рекомендуется применить логарифмирование или Robust Scaling, так как VecNormalize чувствителен к аномальным выбросам в первом же батче обучения.
## Итоговый чек-лист подготовки фичей:

   1. OFI: Log-transform -> Rolling Z-Score.
   2. OBI: Уже в диапазоне $[-1, 1]$, нормализация обычно не нужна.
   3. Spread: Log-transform.
   4. Trade Delta: Как и OFI, через Log + Robust Scale.

Теперь, когда данные готовы и нормализованы, вы готовы запустить обучение. Хотите обсудить архитектуру вознаграждения (Reward Function) специально для маркет-мейкинга, чтобы агент не "сливал" на спреде, а учился его зарабатывать?



====
Для маркет-мейкера (ММ) функция вознаграждения — это не просто прибыль, а баланс между заработком спреда, минимизацией риска инвентаря и избеганием токсичности.
В HFT стандартного PnL недостаточно, так как агент может случайно заработать на тренде, не научившись быть маркет-мейкером. Нам нужна «направляющая» награда.
## 1. Компоненты Reward Function для ММ
Итоговая формула выглядит так:
$$Reward = \Delta PnL_{mm} + \text{Rebate} - \phi \cdot \text{Inventory Risk} - \psi \cdot \text{Adverse Selection}$$ 
## А. $\Delta PnL_{mm}$ (Чистый профит от спреда)
Мы поощряем агента, когда его лимитные ордера исполняются «парами» (купил по Bid, продал по Ask).

* Нюанс: Используйте Mark-to-Market PnL, чтобы агент видел убыток сразу, как только цена пошла против его позиции, не дожидаясь закрытия сделки.

## Б. Rebate Incentive (Бонус за лимит)
Если биржа платит рибейты за Maker-ордера, добавьте их в награду. Это научит агента «бороться» за место в стакане, а не просто бить по рынку.
## В. Inventory Risk Penalty (Штраф за позицию)
Это самое важное. Штраф должен быть нелинейным (квадратичным):
$$\text{Penalty} = \phi \cdot q^2$$ 
Где $q$ — количество монет в позиции.

* Зачем: При маленькой позиции штраф почти незаметен. Но если инвентарь растет, штраф становится огромным, заставляя агента любой ценой (даже в убыток по спреду) сбросить позицию.

## Г. Adverse Selection Penalty (Штраф за «токсичность»)
Если лимитный ордер агента исполнился, а цена через 100мс улетела дальше в ту же сторону — значит, его «переехали» инсайдеры.

* Логика: Штрафуйте агента, если после исполнения его Bid-ордера Micro-price продолжает падать. Это научит модель снимать заявки перед импульсом.

------------------------------
## 2. Реализация на Python для среды Gym

def get_reward(self, current_mid, prev_mid, executed_trades):
    # 1. Mark-to-Market изменение стоимости позиции
    inventory_pnl = self.inventory * (current_mid - prev_mid)
    
    # 2. Прибыль от исполненных лимиток (спред + рибейты)
    trading_pnl = 0
    rebates = 0
    for trade in executed_trades:
        # trade['pnl'] — это разница между ценой ордера и текущим mid
        trading_pnl += trade['pnl']
        rebates += self.maker_rebate_value
        
    # 3. Штраф за инвентарь (Inventory Risk)
    # Коэффициент phi подбирается так, чтобы при макс. позиции штраф 
    # был сопоставим с ожидаемым спредом
    phi = 0.01 
    inventory_penalty = phi * (self.inventory ** 2)
    
    # 4. Собираем всё вместе
    reward = inventory_pnl + trading_pnl + rebates - inventory_penalty
    
    return reward

## 3. Совет по калибровке (Symmetry)
Если агент постоянно «заваливается» в одну сторону (только покупает или только продает), значит штраф за инвентарь слишком слаб.

* Проверка: Посмотрите на распределение inventory в процессе обучения. В идеале оно должно быть похоже на нормальное распределение с центром в нуле.

## 4. Использование "Asymmetric Damping"
Вы можете штрафовать агента сильнее за удержание позиции против направления OFI. Если OFI отрицательный (давление вниз), а агент сидит в лонге — штраф должен удваиваться. Это учит модель не спорить с потоком ордеров.
Каким будет ваш следующий шаг? Мы можем обсудить Hyperparameter Tuning для PPO в контексте этой награды или перейти к вопросам мониторинга обучения, чтобы понять, когда агент действительно "понял" рынок, а когда просто переобучился.



===============

Monitoring an HFT RL agent is different from standard RL (like playing Atari games). Because financial data is extremely noisy, a rising Reward Curve can be a lie—it might just mean the agent caught a lucky trend.
To truly understand if your agent is "learning the market" or just "overfitting to noise," you need to track these HFT-specific metrics in TensorBoard or W&B:
## 1. Inventory Distribution (The "Sanity Check")
The most important metric for a Market Maker.

* What to watch: A histogram of your inventory over time.
* Good behavior: A bell-shaped curve centered at zero.
* Warning sign: If the inventory is constantly at its maximum or minimum limits, your Inventory Penalty is too low, and the agent has turned into a "degenerate trend-follower."

## 2. Fill Rate & Order Life

* Limit Fill Rate: % of your limit orders that get executed.
* Mean Order Life: How long (in ms or ticks) your orders stay in the book before being cancelled.
* Logic: If the Fill Rate is 0%, the agent is "scared" and placing orders too far from the spread. If it's 100%, the agent is being "run over" (Adverse Selection).

## 3. Bid-Ask Spread vs. Volatility

* Metric: Average spread offered by the agent compared to market volatility ($\sigma$).
* Good behavior: The agent should naturally widen its spread when $\sigma$ or OFI volatility increases. If the agent's spread is static, it hasn't learned the relationship between risk and reward.

## 4. Explained Variance (PPO Metric)
This is a standard Stable Baselines3 metric, but critical here.

* What it means: How well the Value Function predicts the rewards.
* In HFT: If Explained Variance is $< 0$ or very low ($< 0.1$), the noise in the Order Book is too high for the model. You likely need better features (like more Attention layers) or more data.

## 5. Selection Bias (Alpha Analysis)
Track the Mid-price movement after an execution:

* Price Change @ T+100ms: If the price moves against you consistently after you get filled, the agent is suffering from Adverse Selection.
* Goal: The Meta-Model or the Attention layers should learn to drop orders before these toxic flow events.

## 6. Strategy "Style" Metrics
Monitor the ratio of Maker vs. Taker trades.

* If your reward function has high fees, the agent should evolve to be 90%+ Maker.
* If the agent is mostly taking (Taker), it’s not market-making; it’s aggressive scalping.

------------------------------
## How to implement this in Stable Baselines3?
Use a Custom Callback. SB3 allows you to hook into every step and log custom variables to TensorBoard.

from stable_baselines3.common.callbacks import BaseCallback
class HFTMonitoringCallback(BaseCallback):
    def __init__(self, verbose=0):
        super(HFTMonitoringCallback, self).__init__(verbose)

    def _on_step(self) -> bool:
        # Access variables from the environment
        env = self.training_env.envs[0] 
        
        # Log to TensorBoard
        self.logger.record("hft/inventory", env.inventory)
        self.logger.record("hft/current_spread", env.last_spread)
        self.logger.record("hft/fill_rate", env.fill_rate)
        
        return True

## The "Pro" Move: Shadow Testing
Once you have a model that looks good on TensorBoard, don't go live. Run it in Shadow Mode:

   1. Stream real-time L2 data.
   2. Let the model "trade" in memory (using your Queue Tracker).
   3. Monitor the Real-time vs. Backtest Tracking Error.

What is the current "Explained Variance" you are seeing in your initial tests? If it’s very low, we can discuss how to increase the Signal-to-Noise ratio in your input features.



