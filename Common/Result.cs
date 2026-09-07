namespace apiprojnew.Common
{
    public class Result<T>
    {
        public int StatusCode { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public Result(int statuscode, string? message, T? data)
        {
            StatusCode = statuscode;
            Message = message;
            Data = data;
        }

        public static Result<T> Ok(T value) => new(200, default, value);
        public static Result<T> BadRequest(string msg) => new(400, msg, default);
        public static Result<T> NotFound(string msg) => new(404, msg, default);
    }
}
