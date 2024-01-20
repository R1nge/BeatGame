using Services.StateMachine.States;

namespace _Assets.Services.StateMachine
{
    public class GameStatesFactory
    {
        public IGameState CreateGameState(GameStateMachine stateMachine)
        {
            return new GameState(stateMachine);
        }
    }
}