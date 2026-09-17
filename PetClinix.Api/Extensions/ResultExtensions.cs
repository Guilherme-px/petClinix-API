using Microsoft.AspNetCore.Mvc;
using PetClinix.BuildingBlocks.Application;

namespace PetClinix.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsFailure)
        {
            return new BadRequestObjectResult(new { result.ErrorCode, result.ErrorMessage });
        }
        return new NoContentResult();
    }
}