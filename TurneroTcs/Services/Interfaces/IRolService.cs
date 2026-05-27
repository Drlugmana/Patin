using Microsoft.AspNetCore.Mvc.Rendering;
using TurneroTcs.Records;

namespace TurneroTcs.Services.Interfaces;

public interface IRolService
{
    Task <IEnumerable<SelectListItem>> GetRolesAsync();

    Task <Result> CreateRoleAsync(string roleName);
}