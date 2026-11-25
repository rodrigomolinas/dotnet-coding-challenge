using dotnet.challenge.api.Models;
using dotnet.challenge.api.Services;
using Microsoft.AspNetCore.Mvc;

namespace dotnet.challenge.api.Controllers;

[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public ProductsController(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
    {
        var product = await _inventory.CreateProductAsync(request.Name, request.Stock);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _inventory.GetProductsAsync();
        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProduct(Guid id)
    {
        var product = await _inventory.GetProductAsync(id);
        if (product is null)
        {
            return NotFound();
        }
        return Ok(product);
    }
}
