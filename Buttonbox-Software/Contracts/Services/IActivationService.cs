namespace Buttonbox_Software.Contracts.Services;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}
