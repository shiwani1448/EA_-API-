using hrms_api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WeatherForecastController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<WeatherForecast>>> GetAll()
        {
            return await _context.WeatherForecasts.ToListAsync();
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<WeatherForecast>> GetById(int id)
        {
            var forecast = await _context.WeatherForecasts.FindAsync(id);

            if (forecast is null)
            {
                return NotFound();
            }

            return forecast;
        }

        [HttpPost]
        public async Task<ActionResult<WeatherForecast>> Create(WeatherForecast forecast)
        {
            _context.WeatherForecasts.Add(forecast);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = forecast.Id }, forecast);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, WeatherForecast forecast)
        {
            if (id != forecast.Id)
            {
                return BadRequest();
            }

            _context.Entry(forecast).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var forecast = await _context.WeatherForecasts.FindAsync(id);

            if (forecast is null)
            {
                return NotFound();
            }

            _context.WeatherForecasts.Remove(forecast);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
