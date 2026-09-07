Критические ошибки

1. Нет таймаута чтения → hung-клиенты навсегда сжигают лимит соединений (DoS).
ProcessClientAsync берёт слот семафора (макс. 100, TcpServer.cs:36,82) и держит его на всё время жизни соединения, затем бесконечно ждёт данные: reader.ReadAsync (TcpServer.cs:89 → TcpCommandParser.cs:44). Клиент, который подключился и не шлёт ничего (или прислал полпакета), держит слот вечно. 100 таких клиентов → все новые блокируются на WaitAsync навсегда, каждый при этом держит открытый сокет. Нет idle-таймаута, нет ReceiveTimeout. Это главный баг доступности.
2. Утечка сокета и семафора в ранних путях ошибки.
try/finally (TcpServer.cs:87-109) покрывает только ReadPipeAsync. Если падает WaitAsync (OCE при остановке, TcpServer.cs:82), CreatePipe (TcpServer.cs:84) или конструктор parser'а — сокет не закрывается, а семафор (уже занятый) не отпускается.
3. Dispose singleton'а ILoggerFactory.
TcpServer.cs:121 _loggerFactory?.Dispose(); — это singleton, созданный DI/Serilog (Program.cs:60). Потребитель не должен dispose-ить сервис контейнера; это ломает логирование всего приложения (зависит от порядка shutdown).
4. Fire-and-forget по общему непотокобезопасному PipeWriter (латентная гонка).
_ = ExecuteCommandAsync(...) (TcpCommandParser.cs:50) + записи GetMemory/Advance/FlushAsync на одном PipeWriter (TcpResponseWriter.cs:36-40). Сейчас срабатывает «по случайности»: все команды мутируют pipe синхронно до первого await, а read-loop однопоточный. Но это хрупко: стоит какой-либо команде сделать await до WriteAsync — кадры повредятся / полетят InvalidOperationException. Небезопасный паттерн по контракту.
5. Хрупкость Advance(totalLength) (TcpResponseWriter.cs:39) — продвигает на переданную длину, а не на фактическую packet.Length; при рассинхроне повредит pipe.

Причины низкой производительности
P1. Включён Nagle (нет NoDelay). new Socket(...) (TcpServer.cs:47) по умолчанию NoDelay=false. Для request/response с мелкими пакетами это +40–200 мс на ответ (Nagle + delayed ACK). Самая дешёвая и impactful поправка по латентности.
P2. Аллокации + лишние копии на каждый ответ. MessageProtocol.Serialize (IMessageProtocol.cs:24-38) создаёт byte[] + MemoryStream + BinaryWriter, затем WriteAsync копирует в память pipe (TcpResponseWriter.cs:36-38) и FlushAsync. Для HFT — давление на GC и лишние memcpy. Нужно сериализовать прямо в GetMemory(...).
P3. Аллокация payload на каждый пакет. TryReadPacket (TcpCommandParser.cs:95) — new byte[payloadLength] + CopyTo на каждый пакет.
P4. Блокировка >100 соединений + открытые сокеты в ожидании. Семафор — это лимит открытых соединений; избыточные клиенты висят в WaitAsync с открытым соктом (см. п.1).
P5 (мелочь). StreamDuplexPipe (PipeReader.Create/PipeWriter.Create над NetworkStream) — лишний буферизующий слой.

