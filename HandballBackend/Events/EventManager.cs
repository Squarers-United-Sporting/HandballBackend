namespace HandballBackend.Events;

public class EventManager(IEventPublisher publisher) : IEventHandler<GameEndEvent> {
    public async Task Handle(GameEndEvent @event) {
        await publisher.Publish(new UpdateElosEvent());
    }
}