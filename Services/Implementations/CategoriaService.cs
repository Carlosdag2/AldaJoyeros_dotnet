using AutoMapper;
using AldaJoyeros.DTOs;
using AldaJoyeros.Entities;
using AldaJoyeros.Repositories.Interfaces;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Services.Implementations
{
    public class CategoriaService : ICategoriaService
    {
        private readonly ICategoriaRepository _categoriaRepository;
        private readonly IMapper _mapper;

        public CategoriaService(ICategoriaRepository categoriaRepository, IMapper mapper)
        {
            _categoriaRepository = categoriaRepository;
            _mapper = mapper;
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

            _mapper.Map(categoriaUpdateDto, categoria);
            var updatedCategoria = await _categoriaRepository.UpdateAsync(categoria);
            return _mapper.Map<CategoriaDto>(updatedCategoria);
        }

        public async Task DeleteAsync(long id)
        {
            if (!await _categoriaRepository.ExistsAsync(id))
            {
                throw new KeyNotFoundException("Categoría no encontrada");
            }

            await _categoriaRepository.DeleteAsync(id);
        }
    }
}
