using Market.Core.Models;
using Market.Core.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Market.Host.Controllers;

[Route("algos")]
[ApiController]
public class AlgosController : ControllerBase
{
    private readonly IAlgoMetadataRepository _algoMetadataRepository;

    public AlgosController(IAlgoMetadataRepository algoMetadataRepository)
    {
        _algoMetadataRepository = algoMetadataRepository;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var values = _algoMetadataRepository.GetAll();
        return Ok(values);
    }

    [HttpPost]
    public IActionResult Create(AlgoMetadata algoMetadata)
    {
        // TODO: Validate metadata
        var saved = _algoMetadataRepository.Save(algoMetadata);
        return !saved ? BadRequest() : Ok();
    }

    [HttpDelete("{strategyId:guid}")]
    public IActionResult Delete(Guid strategyId)
    {
        var _ = _algoMetadataRepository.Delete(strategyId);
        return Ok();
    }
}
