using System;
using System.Reactive.Subjects;

namespace ActivemqNet
{
    public class MessageBus
    {
        private readonly Subject<object> _subject = new Subject<object>();

        internal IObservable<object> MessageStream => _subject;

        public void Publish<TMessage>(TMessage message) where TMessage : class, new() => _subject.OnNext(message);
    }
}