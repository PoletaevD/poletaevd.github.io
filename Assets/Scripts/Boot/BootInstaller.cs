using UnityEngine;
using Zenject;

namespace Clock.Boot
{
    public sealed class BootInstaller : MonoInstaller
    {
        [SerializeField] private string _gameSceneAddress = "clock/gameplay";

        public override void InstallBindings()
        {
            Container.BindInstance(_gameSceneAddress).WithId("GameplayScene");
        }
    }
}
