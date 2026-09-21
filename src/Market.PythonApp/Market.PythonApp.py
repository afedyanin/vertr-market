import redis
import msgpack
from datetime import datetime, timezone

r = redis.Redis(host='localhost', port=6379, db=0)
pubsub = r.pubsub()

pubsub.psubscribe('bars:*')

print("Ожидание баров из C# агрегатора...")

try:
    for message in pubsub.listen():

        if message['type'] != 'pmessage':
            continue
            
        bar_data = msgpack.unpackb(message['data'], use_list=True)
    
        symbol    = bar_data[0] # str
        timestamp = bar_data[1] # int (миллисекунды)
        open_p    = bar_data[2] # float (C-double)
        high      = bar_data[3] # float (C-double)
        low       = bar_data[4] # float (C-double)
        close     = bar_data[5] # float (C-double)
        volume    = bar_data[6] # float (C-double)

        dt = datetime.fromtimestamp(timestamp / 1000.0, tz=timezone.utc)
        print(f"[{dt.strftime('%H:%M:%S.%f')[:-3]}] {symbol} -> Close: {close:.2f}, Vol: {volume:.4f}")

except KeyboardInterrupt:
    print("\nОстановка потребителя.")
    pubsub.punsubscribe('bars:*')