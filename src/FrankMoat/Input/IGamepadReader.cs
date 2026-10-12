namespace FrankMoat.Input;

public interface IGamepadReader
{
    WindowsGamepadSnapshot Read();
}
