---
description: "Интеграция с Centrifugo WebSocket для real-time обновлений в Remote Component Directum RX"
---

# Интеграция с Centrifugo (Real-time)

Настраивает WebSocket-подключение для live-обновлений в RC-компонентах.

## Когда использовать
- Kanban-доска должна обновляться при изменении другим пользователем
- Dashboard с real-time метриками
- Уведомления без polling

## Архитектура
```
[Server: AfterSave handler]
    → DDS Event → AsyncHandler
        → Centrifugo HTTP API → publish(channel, data)
            → WebSocket → [Client: RC Component]
                → React state update → re-render
```

## Алгоритм

### 1. Серверная часть — публикация события

В `ModuleAsyncHandlers.cs`:
```csharp
public virtual void NotifyDealChanged(AsyncHandlerInvokeArgs.NotifyDealChangedInvokeArgs args)
{
    var dealId = args.DealId;
    var deal = Deals.GetAll(d => d.Id == dealId).FirstOrDefault();
    if (deal == null) return;
    
    // Публикация в Centrifugo через HTTP API
    var channel = string.Format("crm:pipeline:{0}", deal.Pipeline.Id);
    var payload = new { dealId = deal.Id, stage = deal.Stage?.Name, amount = deal.Amount };
    
    PublicFunctions.Module.PublishToCentrifugo(channel, "deal_moved", payload);
    
    Logger.WithLogger("CRM").Debug("NotifyDealChanged: published to {0}", channel);
}
```

### 2. Вызов из Saved handler

В `DealHandlers.cs`:
```csharp
public override void Saved(Sungero.Domain.SavedEventArgs e)
{
    if (_obj.State.Properties.Stage.IsChanged)
    {
        var handler = AsyncHandlers.NotifyDealChanged.Create();
        handler.DealId = _obj.Id;
        handler.ExecuteAsync();
    }
}
```

### 3. Клиентская часть — подписка в RC

```typescript
import { useEffect, useState } from 'react';

function useCentrifugoChannel(channel: string) {
    const [lastEvent, setLastEvent] = useState<any>(null);
    
    useEffect(() => {
        // Centrifugo client (подключение через RX proxy)
        const eventSource = new EventSource(
            `/Integration/centrifugo/subscribe?channel=${channel}`
        );
        
        eventSource.onmessage = (event) => {
            const data = JSON.parse(event.data);
            setLastEvent(data);
        };
        
        return () => eventSource.close();
    }, [channel]);
    
    return lastEvent;
}

// Использование в компоненте:
function PipelineKanban({ pipelineId }: Props) {
    const event = useCentrifugoChannel(`crm:pipeline:${pipelineId}`);
    
    useEffect(() => {
        if (event?.type === 'deal_moved') {
            // Обновить конкретную карточку без полной перезагрузки
            updateDealInState(event.dealId, event.stage);
        }
    }, [event]);
}
```

### 4. Конфигурация Centrifugo
Centrifugo уже встроен в RX 26.1. Каналы:
- `crm:pipeline:{id}` — изменения в pipeline
- `crm:deal:{id}` — изменения конкретной сделки
- `crm:notifications:{userId}` — персональные уведомления

## Альтернатива: Polling (если Centrifugo недоступен)
```typescript
useEffect(() => {
    const interval = setInterval(() => {
        fetchPipelineData(pipelineId).then(setData);
    }, 5000); // каждые 5 секунд
    return () => clearInterval(interval);
}, [pipelineId]);
```

## Reference
- Agile Boards: AgileMessageSender + RNDNoticesUtils.dll
- RX 26.1: встроенный Centrifugo endpoint
