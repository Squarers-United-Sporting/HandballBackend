namespace HandballBackend.Events;

public interface IEventPublisher {
    Task Publish<TEvent>(TEvent evt) where TEvent : Event;
}

public class EventPublisher : IEventPublisher {
    private readonly IServiceProvider _container;

    public EventPublisher(IServiceProvider container) {
        _container = container;
    }


    public async Task Publish<TEvent>(TEvent evt) where TEvent : Event {
        using (var scope = _container.CreateScope()) {
            var handlers = scope.ServiceProvider.GetServices<IEventHandler<TEvent>>();
            var tasks = handlers.Select(h => h.Handle(evt)).ToArray();
            await Task.WhenAll(tasks);
        }
    }
}