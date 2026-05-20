using SocialHub.Auth.Application.Common;
using SocialHub.Auth.Application.Dtos;

namespace SocialHub.Auth.Api.Presentation.Http;

internal static class ResultExtensions
{
    public static IResult ToCreated<T>(this ServiceResult<T> result, string location) =>
        result.IsSuccess
            ? Results.Created(location, result.Value)
            : result.ToErrorResult();

    public static IResult ToHttpResult<T>(this ServiceResult<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : result.ToErrorResult();

    private static IResult ToErrorResult<T>(this ServiceResult<T> result)
    {
        var error = new ErrorResponse(result.ErrorCode ?? "error", result.ErrorMessage ?? "Request failed.");

        return result.ErrorCode switch
        {
            "validation_error" => Results.BadRequest(error),
            "duplicate_username" => Results.Conflict(error),
            "duplicate_email" => Results.Conflict(error),
            "invalid_credentials" => Results.Unauthorized(),
            "account_blocked" => Results.Problem(error.Message, statusCode: StatusCodes.Status403Forbidden, title: error.Code),
            "forbidden" => Results.Forbid(),
            "unauthorized" => Results.Unauthorized(),
            "user_not_found" => Results.NotFound(error),
            _ => Results.BadRequest(error)
        };
    }
}
