using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TurneroTcs.Records;
using TurneroTcs.Services.Interfaces;

namespace TurneroTcs.Services;

public class RolService : IRolService
{
    private readonly RoleManager<IdentityRole> _roleManager;

    public RolService(RoleManager<IdentityRole> roleManager)
    {
        _roleManager = roleManager;
    }

    public async Task<IEnumerable<SelectListItem>> GetRolesAsync()
    {
        var roles = await _roleManager.Roles
            .Select(r => new SelectListItem {Value = r.Name, Text = r.Name})
            .ToListAsync();
        
        return roles;
    }

    public async Task<Result> CreateRoleAsync(string nombreRol){

        nombreRol = nombreRol.Trim();
        
        if(string.IsNullOrWhiteSpace(nombreRol)) return Result.Fail("No se ha ingresado un rol.");

        var roleExists = await _roleManager.RoleExistsAsync(nombreRol);
        if(roleExists) return Result.Fail("El rol ya existe.");

        var createRole =  await _roleManager.CreateAsync(new IdentityRole(nombreRol));

        if (!createRole.Succeeded)
        {
            var errors = string.Join("; ", createRole.Errors.Select(e => e.Description));
            return Result.Fail($"Error al crear rol: {errors}");
        }
        return Result.Ok();
    }
}