namespace WebAPI.Communication
{
    public class APIResponse
    {
        public int ErrorCode { get; protected set; }
        public string Message { get; protected set; }

        public object Data { get; protected set; }

        /// <summary>
        /// Successful result
        /// </summary>
        public APIResponse(object data)
        {
            ErrorCode = 0;
            Message = "";
            Data = data;
        }

        /// <summary>
        /// Error result
        /// </summary>
        public APIResponse(int errorCode, string message)
        {
            ErrorCode = errorCode;
            Message = message;
        }

        public static APIResponse FromModelState(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState)
        {
            var result = new APIResponse(400, "Some error");

            if (!modelState.IsValid)
            {
                // Take first error
                foreach (var e in modelState)
                {
                    result = new APIResponse(400, e.Value.Errors[0].ErrorMessage);
                }
            }

            return result;
        }
    }

    public class APIResponse2<T>
    {
        public int ErrorCode { get; protected set; }
        public string Message { get; protected set; }

        public T Data { get; protected set; }

        
        /// <summary>
        /// Successful result
        /// </summary>
        public APIResponse2(T data)
        {
            ErrorCode = 0;
            Message = "";
            Data = data;
        }

        /// <summary>
        /// Error result
        /// </summary>
        public APIResponse2(int errorCode, string message)
        {
            ErrorCode = errorCode;
            Message = message;
        }

        public static APIResponse2<T> FromModelState(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState)
        {
            var result = new APIResponse2<T>(400, "Some error");

            if (!modelState.IsValid)
            {
                // Take first error
                foreach(var e in modelState)
                {
                    result = new APIResponse2<T>(400, e.Value.Errors[0].ErrorMessage);
                }
            }

            return result;
        }

    }
}