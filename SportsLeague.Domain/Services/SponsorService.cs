using Microsoft.Extensions.Logging;
using SportsLeague.Domain.Entities;
using SportsLeague.Domain.Interfaces.Repositories;
using SportsLeague.Domain.Interfaces.Services;

namespace SportsLeague.Domain.Services;

public class SponsorService : ISponsorService
{
    private readonly ISponsorRepository _sponsorRepository;
    private readonly ITournamentSponsorRepository _tournamentSponsorRepository;
    private readonly IGenericRepository<Tournament> _tournamentRepository;
    private readonly ILogger<SponsorService> _logger;

    public SponsorService(
        ISponsorRepository sponsorRepository,
        ITournamentSponsorRepository tournamentSponsorRepository,
        IGenericRepository<Tournament> tournamentRepository,
        ILogger<SponsorService> logger)
    {
        _sponsorRepository = sponsorRepository;
        _tournamentSponsorRepository = tournamentSponsorRepository;
        _tournamentRepository = tournamentRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<Sponsor>> GetAllAsync()
    {
        _logger.LogInformation("Retrieving all sponsors");
        return await _sponsorRepository.GetAllAsync();
    }

    public async Task<Sponsor?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Retrieving sponsor with ID: {SponsorId}", id);
        return await _sponsorRepository.GetByIdAsync(id);
    }

    public async Task<Sponsor> CreateAsync(Sponsor sponsor)
    {
        // Validar nombre duplicado
        var nameExists = await _sponsorRepository.ExistsByNameAsync(sponsor.Name);
        if (nameExists)
            throw new InvalidOperationException(
                $"Ya existe un sponsor con el nombre '{sponsor.Name}'");

        // Validar formato de email
        if (!IsValidEmail(sponsor.ContactEmail))
            throw new InvalidOperationException(
                $"El email '{sponsor.ContactEmail}' no tiene un formato válido");

        _logger.LogInformation("Creating sponsor: {SponsorName}", sponsor.Name);
        return await _sponsorRepository.CreateAsync(sponsor);
    }

    public async Task UpdateAsync(int id, Sponsor sponsor)
    {
        var existing = await _sponsorRepository.GetByIdAsync(id);
        if (existing == null)
            throw new KeyNotFoundException($"No se encontró el sponsor con ID {id}");

        // Validar nombre duplicado excluyendo el actual
        var nameExists = await _sponsorRepository.ExistsByNameAsync(sponsor.Name, id);
        if (nameExists)
            throw new InvalidOperationException(
                $"Ya existe un sponsor con el nombre '{sponsor.Name}'");

        // Validar formato de email
        if (!IsValidEmail(sponsor.ContactEmail))
            throw new InvalidOperationException(
                $"El email '{sponsor.ContactEmail}' no tiene un formato válido");

        existing.Name = sponsor.Name;
        existing.ContactEmail = sponsor.ContactEmail;
        existing.Phone = sponsor.Phone;
        existing.WebsiteUrl = sponsor.WebsiteUrl;
        existing.Category = sponsor.Category;

        _logger.LogInformation("Updating sponsor with ID: {SponsorId}", id);
        await _sponsorRepository.UpdateAsync(existing);
    }

    public async Task DeleteAsync(int id)
    {
        var exists = await _sponsorRepository.ExistsAsync(id);
        if (!exists)
            throw new KeyNotFoundException($"No se encontró el sponsor con ID {id}");

        _logger.LogInformation("Deleting sponsor with ID: {SponsorId}", id);
        await _sponsorRepository.DeleteAsync(id);
    }

    public async Task<TournamentSponsor> AddToTournamentAsync(int sponsorId, TournamentSponsor tournamentSponsor)
    {
        // Validar que el sponsor existe
        var sponsorExists = await _sponsorRepository.ExistsAsync(sponsorId);
        if (!sponsorExists)
            throw new KeyNotFoundException($"No se encontró el sponsor con ID {sponsorId}");

        // Validar que el torneo existe
        var tournamentExists = await _tournamentRepository.ExistsAsync(tournamentSponsor.TournamentId);
        if (!tournamentExists)
            throw new KeyNotFoundException(
                $"No se encontró el torneo con ID {tournamentSponsor.TournamentId}");

        // Validar que no esté duplicado
        var existing = await _tournamentSponsorRepository
            .GetByTournamentAndSponsorAsync(tournamentSponsor.TournamentId, sponsorId);
        if (existing != null)
            throw new InvalidOperationException(
                $"El sponsor ya está vinculado a este torneo");

        // Validar ContractAmount > 0
        if (tournamentSponsor.ContractAmount <= 0)
            throw new InvalidOperationException(
                "El monto del contrato debe ser mayor a 0");

        tournamentSponsor.SponsorId = sponsorId;
        tournamentSponsor.JoinedAt = DateTime.UtcNow;

        _logger.LogInformation("Adding sponsor {SponsorId} to tournament {TournamentId}",
            sponsorId, tournamentSponsor.TournamentId);
        return await _tournamentSponsorRepository.CreateAsync(tournamentSponsor);
    }

    public async Task<IEnumerable<TournamentSponsor>> GetTournamentsBySponsorAsync(int sponsorId)
    {
        _logger.LogInformation("Retrieving tournaments for sponsor {SponsorId}", sponsorId);
        return await _tournamentSponsorRepository.GetBySponsorIdAsync(sponsorId);
    }

    public async Task RemoveFromTournamentAsync(int sponsorId, int tournamentId)
    {
        var existing = await _tournamentSponsorRepository
            .GetByTournamentAndSponsorAsync(tournamentId, sponsorId);
        if (existing == null)
            throw new KeyNotFoundException(
                $"No se encontró la vinculación entre el sponsor {sponsorId} y el torneo {tournamentId}");

        _logger.LogInformation("Removing sponsor {SponsorId} from tournament {TournamentId}",
            sponsorId, tournamentId);
        await _tournamentSponsorRepository.DeleteAsync(existing.Id);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}