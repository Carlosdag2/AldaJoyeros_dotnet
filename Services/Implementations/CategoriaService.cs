using Microsoft.EntityFrameworkCore;
using AldaJoyeros.Mappings;
using AldaJoyeros.DTOs;
using AldaJoyeros.Entities;
using AldaJoyeros.Repositories.Interfaces;
using AldaJoyeros.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AldaJoyeros.Services.Implementations
{
    public class CategoriaService : ICategoriaService
    {
        private readonly ICategoriaRepository _categoriaRepository;
        private readonly AppMapper _mapper;
        private readonly ILogger<CategoriaService> _logger;
        private readonly AldaJoyeros.Data.AldaJoyerosContext? _sql;
        private readonly bool _optimized;
        private Task<List<CategoriaDto>>? _summaries;

        public CategoriaService(
            ICategoriaRepository categoriaRepository, 
            AppMapper mapper,
            ILogger<CategoriaService> logger, AldaJoyeros.Data.AldaJoyerosContext? sql = null, Microsoft.Extensions.Options.IOptions<AldaJoyeros.Configuration.StorageOptimizationOptions>? optimization = null)
        {
            _categoriaRepository = categoriaRepository;
            _mapper = mapper;
            _logger = logger; _sql = sql; _optimized = optimization?.Value.Enabled ?? true;
        }

        public async Task<IEnumerable<CategoriaDto>> GetAllAsync()
        {
            if (_optimized && _sql != null)
            {
                var rows = await (_summaries ??= _sql.Categorias.AsNoTracking().Select(c => new CategoriaDto {
                    Id = c.Id, Nombre = c.Nombre, CantidadProductos = c.Productos.Count(p => !p.Eliminado)
                }).ToListAsync());
                // Controllers add public slugs and family counts; don't share their mutable DTOs.
                return rows.Select(c => new CategoriaDto { Id=c.Id, Nombre=c.Nombre, CantidadProductos=c.CantidadProductos }).ToArray();
            }
            var categorias = await _categoriaRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<CategoriaDto>>(categorias);
        }

        public async Task<CategoriaDto?> GetByIdAsync(long id)
        {
            if (_optimized && _sql != null) return (await GetAllAsync()).FirstOrDefault(c => c.Id == id);
            var categoria = await _categoriaRepository.GetByIdAsync(id);
            return categoria == null ? null : _mapper.Map<CategoriaDto>(categoria);
        }

        public async Task<CategoriaDto> CreateAsync(CategoriaCreateDto categoriaCreateDto)
        {
            _summaries = null;
            if (await _categoriaRepository.NombreExistsAsync(categoriaCreateDto.Nombre))
            {
                throw new InvalidOperationException("Ya existe una categoría con ese nombre");
            }

            var categoria = _mapper.Map<Categoria>(categoriaCreateDto);
            var createdCategoria = await _categoriaRepository.CreateAsync(categoria);
            return _mapper.Map<CategoriaDto>(createdCategoria);
        }

        public async Task<CategoriaDto> UpdateAsync(long id, CategoriaUpdateDto categoriaUpdateDto)
        {
            _summaries = null;
            var categoria = await _categoriaRepository.GetByIdAsync(id);
            if (categoria == null)
            {
                throw new KeyNotFoundException("Categoría no encontrada");
            }

            // No permitir renombrar a "Sin categoría" (reservado)
            if (categoriaUpdateDto.Nombre == "Sin categoría" && categoria.Nombre != "Sin categoría")
            {
                throw new InvalidOperationException("El nombre 'Sin categoría' está reservado para el sistema");
            }

            _mapper.Map(categoriaUpdateDto, categoria);
            var updatedCategoria = await _categoriaRepository.UpdateAsync(categoria);
            return _mapper.Map<CategoriaDto>(updatedCategoria);
        }

        public async Task DeleteAsync(long id)
        {
            _summaries = null;
            var categoria = await _categoriaRepository.GetByIdAsync(id);
            if (categoria == null)
            {
                throw new KeyNotFoundException("Categoría no encontrada");
            }

            // No permitir eliminar la categoría "Sin categoría"
            if (categoria.Nombre == "Sin categoría")
            {
                throw new InvalidOperationException("No se puede eliminar la categoría 'Sin categoría' porque es la categoría por defecto del sistema");
            }

            // Si la categoría tiene productos, reasignarlos a "Sin categoría"
            if (categoria.Productos != null && categoria.Productos.Any())
            {
                var defaultCategory = await _categoriaRepository.GetOrCreateDefaultCategoryAsync();
                
                _logger.LogInformation(
                    "Reasignando {Count} productos de categoría '{From}' (ID: {FromId}) a '{To}' (ID: {ToId})",
                    categoria.Productos.Count,
                    categoria.Nombre,
                    categoria.Id,
                    defaultCategory.Nombre,
                    defaultCategory.Id);

                await _categoriaRepository.ReassignProductsAsync(id, defaultCategory.Id);
            }

            await _categoriaRepository.DeleteAsync(id);
        }

        /// <summary>
        /// Verifica si una categoría es la categoría por defecto del sistema
        /// </summary>
        public async Task<bool> IsDefaultCategoryAsync(long id)
        {
            var categoria = await _categoriaRepository.GetByIdAsync(id);
            return categoria?.Nombre == "Sin categoría";
        }
    }
}
