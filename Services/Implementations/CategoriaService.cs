using AutoMapper;
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
        private readonly IMapper _mapper;
        private readonly ILogger<CategoriaService> _logger;

        public CategoriaService(
            ICategoriaRepository categoriaRepository, 
            IMapper mapper,
            ILogger<CategoriaService> logger)
        {
            _categoriaRepository = categoriaRepository;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<CategoriaDto>> GetAllAsync()
        {
            var categorias = await _categoriaRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<CategoriaDto>>(categorias);
        }

        public async Task<CategoriaDto?> GetByIdAsync(long id)
        {
            var categoria = await _categoriaRepository.GetByIdAsync(id);
            return categoria == null ? null : _mapper.Map<CategoriaDto>(categoria);
        }

        public async Task<CategoriaDto> CreateAsync(CategoriaCreateDto categoriaCreateDto)
        {
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
