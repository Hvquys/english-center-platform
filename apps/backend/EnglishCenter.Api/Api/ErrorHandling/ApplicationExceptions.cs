namespace EnglishCenter.Api.Api.ErrorHandling;

public abstract class ApplicationExceptionBase(string message) : Exception(message);

public sealed class ResourceNotFoundException(string message) : ApplicationExceptionBase(message);

public sealed class ResourceConflictException(string message) : ApplicationExceptionBase(message);

public sealed class RequestValidationException(
    string message,
    IReadOnlyDictionary<string, string[]> errors) : ApplicationExceptionBase(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
