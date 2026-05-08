namespace NexaPay.Application.Common.Models
{
    public enum ResultErrorType { None, NotFound, BusinessRule }

    // Kontrakt för AuditBehavior och andra pipeline-komponenter.
    public interface IResult
    {
        bool IsSuccess { get; }
        string Error { get; }
        ResultErrorType ErrorType { get; }
    }

    public class Result : IResult
    {
        protected Result(bool isSuccess, string error, ResultErrorType errorType = ResultErrorType.None)
        {
            if (isSuccess && error != string.Empty)
                throw new InvalidOperationException("Ett lyckat resultat kan inte ha ett felmeddelande");

            if (!isSuccess && error == string.Empty)
                throw new InvalidOperationException("Ett misslyckat resultat måste ha ett felmeddelande");

            IsSuccess = isSuccess;
            Error = error;
            ErrorType = errorType;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }
        public ResultErrorType ErrorType { get; }

        public static Result Success() => new(true, string.Empty);
        public static Result Failure(string error) => new(false, error, ResultErrorType.BusinessRule);
        public static Result NotFound(string error) => new(false, error, ResultErrorType.NotFound);
    }

    public class Result<T> : Result
    {
        private Result(bool isSuccess, string error, T? value, ResultErrorType errorType = ResultErrorType.None)
            : base(isSuccess, error, errorType)
        {
            Value = value;
        }

        public T? Value { get; }

        public static Result<T> Success(T value) => new(true, string.Empty, value);
        public static new Result<T> Failure(string error) => new(false, error, default, ResultErrorType.BusinessRule);
        public static new Result<T> NotFound(string error) => new(false, error, default, ResultErrorType.NotFound);
    }
}
