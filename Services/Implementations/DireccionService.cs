using AutoMapper;
using AldaJoyeros.DTOs;
using AldaJoyeros.Entities;
using AldaJoyeros.Repositories.Interfaces;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Services.Implementations
{
    public class DireccionService : IDireccionService
    {
        private readonly IDireccionRepository _direccionRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IMapper _mapper;

        public DireccionService(
            IDireccionRepository direccionRepository,
            IUsuarioRepository usuarioRepository,
            IMapper mapper)
        {
            _direccionRepository = direccionRepository;
            _usuarioRepository = usuarioRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<DireccionDto>> GetAllAsync()
        {
            var direcciones = await _direccionRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<DireccionDto>>(direcciones);
        }

        public async Task<DireccionDto?> GetByIdAsync(long id)
        {
            var direccion = await _direccionRepository.GetByIdAsync(id);
            return direccion == null ? null : _mapper.Map<DireccionDto>(direccion);
        }

        public async Task<IEnumerable<DireccionDto>> GetByUsuarioIdAsync(long usuarioId)
        {
            var direcciones = await _direccionRepository.GetByUsuarioIdAsync(usuarioId);
            return _mapper.Map<IEnumerable<DireccionDto>>(direcciones);
        }

        public async Task<DireccionDto> CreateAsync(long usuarioId, DireccionCreateDto direccionCreateDto)
        {
            if (!await _usuarioRepository.ExistsAsync(usuarioId))
            {
                throw new KeyNotFoundException("Usuario no encontrado");
            }

            var direccion = new Direccion
            {
                Calle = direccionCreateDto.Calle,
                Numero = direccionCreateDto.Numero,
                Piso = direccionCreateDto.Piso,
                CodigoPostal = direccionCreateDto.CodigoPostal,
                Ciudad = direccionCreateDto.Ciudad,
                Provincia = direccionCreateDto.Provincia,
                UsuarioId = usuarioId
            };

            var createdDireccion = await _direccionRepository.CreateAsync(direccion);
            return _mapper.Map<DireccionDto>(createdDireccion);
        }

        public async Task<DireccionDto> UpdateAsync(long id, DireccionUpdateDto direccionUpdateDto)
        {
            var direccion = await _direccionRepository.GetByIdAsync(id);
            if (direccion == null)
            {
                throw new KeyNotFoundException("Dirección no encontrada");
            }

            direccion.Calle = direccionUpdateDto.Calle;
            direccion.Numero = direccionUpdateDto.Numero;
            direccion.Piso = direccionUpdateDto.Piso;
            direccion.CodigoPostal = direccionUpdateDto.CodigoPostal;
            direccion.Ciudad = direccionUpdateDto.Ciudad;
            direccion.Provincia = direccionUpdateDto.Provincia;

            var updatedDireccion = await _direccionRepository.UpdateAsync(direccion);
            return _mapper.Map<DireccionDto>(updatedDireccion);
        }

        public async Task DeleteAsync(long id)
        {
            if (!await _direccionRepository.ExistsAsync(id))
            {
                throw new KeyNotFoundException("Dirección no encontrada");
            }

            await _direccionRepository.DeleteAsync(id);
        }
    }
}
