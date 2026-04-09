Расчет статистик для потоковых временных рядов (Time Series streams) требует подходов, которые не хранят весь набор данных в памяти и обрабатывают каждое значение за один проход. [1, 2] 

Ниже приведены основные стратегии:
## 1. Инкрементальные (Online) алгоритмы
Эти методы обновляют статистику при поступлении каждого нового значения без пересчета всей истории. [1] 

* Метод Велфорда (Welford's Method): Золотой стандарт для расчета среднего и дисперсии. Он численно стабилен и требует хранения только нескольких переменных (текущее среднее, количество элементов и сумма квадратов отклонений).
* Инкрементальные моменты: Обобщение для расчета асимметрии (skewness) и эксцесса (kurtosis) в реальном времени. [3, 4, 5, 6] 

## 2. Оконные функции (Windowing)
Статистика считается не по всей истории, а по определенному интервалу времени или количеству точек. [7, 8] 

* Tumbling Window (Кувыркающееся окно): Окно фиксированного размера, которое перемещается прыжками (без перекрытий). Удобно для периодических отчетов (например, среднее каждые 5 минут).
* Sliding Window (Скользящее окно): Окно, которое сдвигается с каждым новым событием. Позволяет видеть актуальную статистику в любой момент времени (например, скользящее среднее за последние 100 тиков).
* Session Window (Сессионное окно): Группирует данные по периодам активности, разделенным паузами. [7, 8] 

## 3. Экспоненциальное сглаживание (Exponential Smoothing)
Подход, где «вес» старых данных экспоненциально убывает со временем. [9] 

* EWMA (Exponentially Weighted Moving Average): Позволяет учитывать тренд, отдавая приоритет последним значениям. Не требует хранения истории, только одного значения предыдущего состояния.

## 4. Вероятностные структуры данных (Sketches)
Используются, когда точный расчет невозможен из-за огромного объема данных. [2] 

* t-Digest / GK-Array: Для быстрой аппроксимации квантилей и медианы на потоке (обычная медиана требует сортировки, что невозможно в стриме без хранения всех данных).
* Count-Min Sketch: Для оценки частоты появления конкретных значений в потоке. [2] 

## 5. Потоковая сегментация (Change Point Detection)
Алгоритмы, определяющие, когда статистические характеристики ряда (среднее или волатильность) существенно изменились. [10, 11] 

* CUSUM: Алгоритм накопленных сумм для обнаружения разладки ряда.
* ClaSS: Современный алгоритм для сегментации потока данных с сенсоров. [10, 11] 

Какой из подходов наиболее актуален для вашей задачи? Мы можем углубиться в алгоритм Велфорда для дисперсии или разобрать, как работают t-Digests для расчета процентилей.

[1] [https://statistics.unl.edu](https://statistics.unl.edu/sites/unl.edu.ianr.statistics/files/media/file/Streaming%20data%20survey.pdf)
[2] [https://www.youtube.com](https://www.youtube.com/watch?v=-4wXOw25m5E)
[3] [https://www.geeksforgeeks.org](https://www.geeksforgeeks.org/web-tech/expression-for-mean-and-variance-in-a-running-stream/)
[4] [https://www.nowozin.net](https://www.nowozin.net/sebastian/blog/streaming-mean-and-variance-computation.html)
[5] [https://medium.com](https://medium.com/data-science/efficiently-computing-the-variance-in-massive-and-distributed-datasets-c1f9fc1a13e3)
[6] [https://www.youtube.com](https://www.youtube.com/watch?v=RMNve6ZqhHo&t=1)
[7] [https://www.cs.ucr.edu](http://www.cs.ucr.edu/~eamonn/icdm-01.pdf)
[8] [https://www.kaggle.com](https://www.kaggle.com/code/egorkainov/timeseries-forecasting-tutorial)
[9] [https://www.tigerdata.com](https://www.tigerdata.com/blog/time-series-analysis-what-is-it-how-to-use-it)
[10] [https://arxiv.org](https://arxiv.org/abs/2310.20431)
[11] [https://www.oil-industry.net](https://www.oil-industry.net/SD_PDF/2026/03/69%20TRIZ%20Yudin.pdf)

