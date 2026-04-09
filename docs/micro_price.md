Micro-price — это более точная оценка «справедливой» цены актива (fair value), чем простая средняя цена между Bid и Ask (Mid-price). Она учитывает не только цены, но и дисбаланс объемов на лучших уровнях стакана.
Основная идея: если на стороне покупки (Bid) стоит огромный объем, а на стороне продажи (Ask) — крошечный, цена с большой вероятностью пойдет вверх. Micro-price отражает это ожидание.
## 1. Базовая формула (Weighted Mid-price)
Самый простой вариант — взвесить цены по объемам противоположных сторон:
$$P_{micro} = \frac{P_{bid} \cdot V_{ask} + P_{ask} \cdot V_{bid}}{V_{bid} + V_{ask}}$$ 
Где:

* $P_{bid}, P_{ask}$ — лучшие цены покупки и продажи.
* $V_{bid}, V_{ask}$ — объемы на этих уровнях.

Логика: Чем больше объем на Bid ($V_{bid}$), тем ближе Micro-price прижимается к цене Ask, сигнализируя о давлении покупателей.
------------------------------
## 2. Стохастический Micro-price (Модель Стоикова)
Классический Micro-price (из работ Саши Стоикова) — это математическое ожидание будущей цены после того, как в стакане произойдет следующее изменение (тик).
В этой модели Micro-price определяется как:
$$M_t = E[P_{mid, \tau} | \mathcal{I}_t]$$ 
Где $\tau$ — момент выхода цены из текущего состояния, а $\mathcal{I}_t$ — текущий дисбаланс (imbalance) и спред.
Как это считается на практике:

   1. Дискретизация дисбаланса: Весь диапазон дисбаланса $I = \frac{V_{bid}}{V_{bid} + V_{ask}}$ (от 0 до 1) разбивается на корзины (например, 10 или 100 групп).
   2. Матрица переходов: Строится марковская цепь, которая описывает вероятность перехода из текущего состояния (спред $S$, дисбаланс $I$) в состояние, где цена изменилась.
   3. Решение системы: Micro-price вычисляется через решение системы линейных уравнений, связывающих текущий дисбаланс с вероятностью движения цены вверх или вниз.

------------------------------
## Почему Micro-price лучше Mid-price?

   1. Опережающий характер: Он начинает двигаться раньше, чем происходит реальный перескок котировки (Price Improvement).
   2. Фильтрация шума: Обычный Mid-price прыгает дискретно (на пол-тика), а Micro-price меняется плавно при каждом изменении объема.
   3. HFT стратегии: Роботы используют его как краткосрочный сигнал для входа в позицию перед тем, как «толпа» исполнит лимитные ордера.

## Реализация на Python (псевдокод)

def calculate_micro_price(bid_price, bid_vol, ask_price, ask_vol):
    imbalance = bid_vol / (bid_vol + ask_vol)
    # Взвешенная формула
    micro_price = bid_price * (1 - imbalance) + ask_price * imbalance
    return micro_price

Вам интересно посмотреть на матричный метод расчета (модель Стоикова через марковские цепи) или достаточно взвешенной формулы для реализации в стриме?


=======================

Стратегии на основе Micro-price обычно относятся к категории высокочастотной торговли (HFT) или внутридневного маркет-мейкинга. Основная идея — использовать разницу между текущей средней ценой (Mid-price) и «справедливой» ценой (Micro-price) как сигнал о краткосрочном импульсе.
Вот основные подходы:
## 1. Предсказание движения цены (Alpha Signal)
Micro-price является опережающим индикатором. Если $P_{micro} > P_{mid}$, это сигнал о том, что в стакане преобладает объем на покупку, и следующая сделка с высокой вероятностью сдвинет цену вверх.

* Логика: Вход в лонг, когда Micro-price стабильно выше Mid-price на определенный порог.
* Цель: Забрать 1–2 тика движения.

## 2. Улучшенный Маркет-мейкинг (Skewing)
Маркет-мейкеры выставляют котировки с обеих сторон (Bid и Ask). Micro-price помогает динамически управлять этими уровнями:

* Логика: Если $P_{micro}$ растет, алгоритм «подтягивает» свои заявки выше (увеличивает цену Bid и Ask), чтобы не быть исполненным по невыгодной цене «токсичным» потоком ордеров.
* Результат: Снижение риска неблагоприятного отбора (Adverse Selection).

## 3. Оптимизация исполнения (Execution/VWAP)
Для крупных игроков важно купить или продать большой объем с минимальным влиянием на рынок (Market Impact).

* Логика: Алгоритм исполнения (например, POV или TWAP) ждет моментов, когда Micro-price благоприятен. Например, покупка совершается только тогда, когда $P_{micro}$ ниже $P_{mid}$, что означает временное отсутствие давления покупателей.
* Результат: Экономия на проскальзывании (Slippage).

## 4. Арбитраж дисбаланса (Imbalance Arbitrage)
Стратегия основана на поиске отклонений между Micro-price на разных площадках для одного и того же актива.

* Логика: Если на одной бирже $P_{micro}$ уже пошел вверх, а на другой Mid-price еще стоит на месте, алгоритм покупает там, где цена еще не отреагировала.

## 5. Предиктивное выставление лимитных ордеров
Вместо покупки «по рынку» (Market Order) с уплатой спреда, трейдер пытается встать лимитным ордером.

* Логика: Micro-price помогает понять, на каком уровне лучше встать в очередь. Если $P_{micro}$ прижат к Ask, вставать в конец очереди на Bid бесполезно — цена скорее уйдет вверх, не исполнив вас. В этом случае эффективнее ударить по рынку или встать на уровень выше.

------------------------------
Ключевые риски:

* Задержки (Latency): Если ваш расчет Micro-price медленнее, чем у конкурентов, сигнал станет «токсичным».
* Спуфинг (Spoofing): Крупные игроки могут выставлять фиктивные объемы, чтобы искусственно завысить Micro-price и спровоцировать алгоритмы на покупку.

Хотите разобрать, как фильтровать сигналы Micro-price, чтобы не попадаться на спуфинг?

=======================

To filter out spoofing (fake liquidity designed to manipulate the Micro-price), you need to move beyond simple volume weighting and look at the dynamics of the order book.
Here are the most effective technical approaches to filtering these signals:
## 1. Volume Decaying (Weighting by Depth)
Spoofers often place large orders at the best Bid/Ask or just one tick behind to move the Micro-price without the risk of being filled.

* The Filter: Instead of using only the top-level $V_{bid}$ and $V_{ask}$, use a weighted average of multiple levels (e.g., top 5 levels).
* Logic: It is much harder and more expensive for a spoofer to maintain fake large blocks across the entire depth of the book than at just the touch.

## 2. Order "Age" and Stability Filter
Real liquidity tends to sit in the book longer. Spoof orders are often canceled and replaced frequently to avoid execution.

* The Filter: Apply a "persistence" weight. Assign higher weight to volumes that have been resting at a price level for $> X$ milliseconds.
* Calculation: $V_{filtered} = V_{current} \times \text{StabilityFactor}$. If a large block appears instantly and the Micro-price jumps, ignore it for the first $N$ ticks.

## 3. Correlation with Executions (Tape Sync)
A true imbalance in the Micro-price should eventually lead to aggressive market orders (trades) in that direction.

* The Filter: Compare the Micro-price shift with the Time & Sales (Tape).
* Logic: If $P_{micro}$ is pushing higher due to a massive Bid, but all actual trades are hitting the Bid (selling), the Bid is likely "fake" or "passive" and will soon be pulled.
* Signal: Only trust a Micro-price move if it is accompanied by "aggressive" buying (trades at the Ask).

## 4. Cancellation Rate Monitoring
High-frequency traders monitor the ratio of Cancellations to Limit Orders.

* The Filter: If you see a sudden spike in $V_{bid}$ followed by a high rate of cancellations (Limit-to-Cancel ratio drops), flag that side as "toxic" or "spoofed."
* Action: Temporarily "freeze" the Micro-price calculation or shift the weight toward the side with more stable, filled orders.

## 5. Hidden Liquidity (Icebergs) Detection
Sometimes the Micro-price is wrong because there is "hidden" selling pressure (Iceberg orders) at the Ask that isn't reflected in the displayed volume.

* The Filter: If the Micro-price is very high (strong Bid), but the Ask price refuses to move despite multiple market buy orders, there is an Iceberg.
* Adjustment: Manually increase the "effective" $V_{ask}$ in your formula to account for the hidden resistance.

## Summary Checklist for a Robust Micro-price:

   1. Don't trust the first level alone (use Level 2/Deep Book).
   2. Add a time-delay (don't react to volume changes faster than $X$ ms).
   3. Cross-reference with trades (is the volume actually being "consumed" or just "shown"?).

Would you like to see a Python snippet demonstrating how to calculate a "Decayed" Micro-price using multiple levels of the Order Book?

=========

Decayed Micro-price (или "Weighted-Level Micro-price") — это усовершенствованная версия базового Micro-price, которая учитывает не только лучшие Bid/Ask (Level 1), но и более глубокие уровни стакана (Level 2+).
Основная идея в том, что объемы на первом уровне (Top of the Book) наиболее подвержены спуфингу и шуму. Чем глубже уровень, тем сложнее и дороже манипулировать им, но тем меньше его прямое влияние на моментальное изменение цены.
## Как его рассчитать?
Вместо использования только $V_{bid1}$ и $V_{ask1}$, мы берем сумму объемов на нескольких уровнях, применяя коэффициент затухания (decay). Чем глубже уровень, тем меньше его вес.
## Формула:

   1. Рассчитываем совокупный взвешенный объем для Bid и Ask:
   $$V_{bid}^{total} = \sum_{i=1}^{n} V_{bid,i} \cdot e^{-\lambda(i-1)}$$ $$V_{ask}^{total} = \sum_{i=1}^{n} V_{ask,i} \cdot e^{-\lambda(i-1)}$$ 
   * $i$ — номер уровня (1, 2, 3...).
      * $\lambda$ — коэффициент затухания (чем он выше, тем меньше мы верим "глубине").
   2. Рассчитываем средневзвешенную цену (аналогично Mid-price, но по всем уровням):
   $$P_{bid}^{avg} = \frac{\sum P_{bid,i} \cdot w_i}{\sum w_i}, \quad P_{ask}^{avg} = \frac{\sum P_{ask,i} \cdot w_i}{\sum w_i}$$ 
   3. Итоговый Decayed Micro-price:
   $$P_{decayed\_micro} = \frac{P_{bid}^{avg} \cdot V_{ask}^{total} + P_{ask}^{avg} \cdot V_{bid}^{total}}{V_{bid}^{total} + V_{ask}^{total}}$$ 

------------------------------
## Реализация на Python

import numpy as np
def calculate_decayed_micro_price(bids, asks, decay=0.5):
    """
    bids, asks: списки кортежей (price, volume)
    decay: коэффициент затухания (lambda)
    """
    def get_weighted_stats(levels):
        total_v = 0
        weighted_p = 0
        for i, (p, v) in enumerate(levels):
            weight = np.exp(-decay * i)
            total_v += v * weight
            weighted_p += p * (v * weight)
        
        return weighted_p / total_v if total_v > 0 else 0, total_v

    avg_p_bid, total_v_bid = get_weighted_stats(bids)
    avg_p_ask, total_v_ask = get_weighted_stats(asks)

    # Итоговая формула взвешивания сторон
    micro_price = (avg_p_bid * total_v_ask + avg_p_ask * total_v_bid) / (total_v_bid + total_v_ask)
    return micro_price
# Пример данных (Level 3)bids = [(100.0, 10), (99.9, 50), (99.8, 100)]asks = [(100.1, 5), (100.2, 20), (100.3, 40)]

print(f"Decayed Micro-price: {calculate_decayed_micro_price(bids, asks):.4f}")

## Почему это работает против спуфинга?

   1. Устойчивость: Если спуфер "подставит" огромный объем на Bid1, обычный Micro-price резко взлетит. Decayed Micro-price отреагирует спокойнее, так как он сбалансирован реальными объемами на уровнях 2, 3 и т.д.
   2. Информативность: Он учитывает "стенки" (большие лимитки), которые стоят чуть дальше спреда и реально сдерживают цену.

Хотите узнать, как подобрать оптимальный коэффициент затухания ($\lambda$) под конкретный волатильный инструмент?

=======

Подбор оптимального коэффициента затухания $\lambda$ (lambda) — это задача калибровки модели под конкретный рынок или инструмент. Единого «магического» числа не существует, так как ликвидность на акциях (например, AAPL) и на криптовалютах (например, BTC) ведет себя по-разному. [1, 2] 
Вот основные методы подбора и оптимизации этого параметра:
## 1. Метод минимизации ошибки предсказания (MSE)
Самый распространенный способ — это «backtesting» на исторических данных стакана. [3] 

* Целевая переменная: Будущий Mid-price через $N$ тиков (например, через 5 или 10 изменений цены).
* Процесс: Вы запускаете расчет Decayed Micro-price с разными значениями $\lambda$ (например, от 0.1 до 2.0 с шагом 0.1) и сравниваете его с реальной ценой в будущем.
* Оптимум: То значение $\lambda$, при котором среднеквадратичная ошибка (MSE) между $P_{decayed\_micro}$ и будущим $P_{mid}$ минимальна.

## 2. Анализ «информационного горизонта»
$\lambda$ определяет, какую часть стакана вы считаете «информативной». [4, 5] 

* Высокая $\lambda$ (например, > 1.0): Вес быстро падает. Вы доверяете только первым 1–2 уровням. Это подходит для инструментов с очень узким спредом и огромной ликвидностью на первом уровне (толстые стаканы).
* Низкая $\lambda$ (например, < 0.3): Вы учитываете глубокие уровни. Это лучше работает на «тонких» стаканах, где первый уровень легко пробивается, и реальное сопротивление (стены) стоит глубже.

## 3. Адаптивная калибровка по волатильности
В моменты высокой волатильности стакан становится «дырявым», а заявки на первом уровне — крайне нестабильными. [2, 6] 

* Стратегия: Динамически увеличивать $\lambda$ при росте волатильности. В спокойное время можно смотреть глубже (низкая $\lambda$), а в моменты хаоса — фокусироваться только на ближайших ценах, так как глубокие уровни могут не успевать обновляться (latency). [7] 

## 4. Корреляция с потоком ордеров (Order Flow)
Вы можете подбирать $\lambda$, исходя из того, на каких уровнях чаще всего происходят реальные исполнения (Trades). [8] 

* Если 90% рыночных ордеров исполняются только об первый уровень, $\lambda$ должна быть высокой.
* Если часто происходят крупные «прострелы», забирающие 5–10 уровней, $\lambda$ должна быть ниже, чтобы Micro-price заранее учитывал плотность этих уровней.

## Эмпирические значения для старта:

* Ликвидные акции / Голубые фишки: $\lambda \approx 0.5 - 0.8$. Первый уровень важен, но 2–3 уровни дают необходимый контекст.
* Криптовалюты (Top-tier): $\lambda \approx 0.3 - 0.5$. Из-за высокой фрагментации и манипуляций на первом уровне полезно смотреть глубже.
* Неликвидные активы: $\lambda \approx 0.1 - 0.2$. Первый уровень часто пуст или случаен, нужно усреднять по большой глубине.

Рекомендуемый следующий шаг: Написать скрипт для проведения Grid Search (перебора по сетке), который прогонит исторический лог стакана и найдет $\lambda$ с лучшей предсказательной силой для вашего таймфрейма. Хотите пример кода для такого валидатора?

[1] [https://www.une.edu.au](https://www.une.edu.au/__data/assets/pdf_file/0009/76464/unebsop14-1.pdf)
[2] [https://www.researchgate.net](https://www.researchgate.net/publication/282240091_What_should_the_value_of_lambda_be_in_the_exponentially_weighted_moving_average_volatility_model)
[3] [https://www.mdpi.com](https://www.mdpi.com/2073-445X/10/11/1238)
[4] [https://medium.com](https://medium.com/@mhfizt/high-frequency-estimator-of-future-prices-micro-price-paper-code-walkthrough-475adb98e91d)
[5] [https://medium.com](https://medium.com/@writeronepagecode/quant-trading-with-python-a-guide-to-limit-order-book-analysis-ep-2-365-8db2e017a623#:~:text=In%20a%20quant%2Dtrading%20context%20this%20is%20typically,what%20features%20are%20fed%20into%20the%20strategy.)
[6] [https://www.tandfonline.com](https://www.tandfonline.com/doi/full/10.1080/00036846.2014.982853)
[7] [https://www.investopedia.com](https://www.investopedia.com/terms/l/lambda.asp#:~:text=Table_title:%20Interpreting%20Lambda%20in%20Options%20Trading%20Table_content:,the%20specific%20option%20and%20market%20conditions%20%7C)
[8] [https://arxiv.org](https://arxiv.org/html/2507.22712v1)





