using Clock.TimeSync;
using UnityEngine;
using Zenject;

namespace Clock.Presentation
{
    public sealed class ClockGameInstaller : MonoInstaller
    {
        [SerializeField] private ClockController _controller;

        public override void InstallBindings()
        {
            Container.BindInstance(new ServerClock());
            Container.BindInstance(new TimeApiService());
            Container.Bind<IClockTimeSource>().FromInstance(_controller);
        }
    }
}
