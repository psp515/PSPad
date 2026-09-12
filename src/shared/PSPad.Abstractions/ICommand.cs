namespace PSPad.Abstractions;

public interface ICommand
{
    Guid CommandId { get; }
    Guid UserId { get; }
}
