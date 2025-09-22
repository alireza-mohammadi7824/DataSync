using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Monitoring.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace Monitoring.Alerts;

public sealed class AlertPolicyAppService :
    CrudAppService<AlertPolicy, AlertPolicyDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateAlertPolicyDto>,
    IAlertPolicyAppService
{
    public AlertPolicyAppService(IRepository<AlertPolicy, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MonitoringPermissions.AlertPolicies.View;
        GetListPolicyName = MonitoringPermissions.AlertPolicies.View;
        CreatePolicyName = MonitoringPermissions.AlertPolicies.Create;
        UpdatePolicyName = MonitoringPermissions.AlertPolicies.Edit;
        DeletePolicyName = MonitoringPermissions.AlertPolicies.Delete;
    }

    protected override async Task<AlertPolicy> MapToEntityAsync(CreateUpdateAlertPolicyDto createInput)
    {
        await ValidateDtoAsync(createInput);
        var emails = NormalizeEmails(createInput.Emails);
        var emailString = string.Join(";", emails);

        return new AlertPolicy(
            GuidGenerator.Create(),
            createInput.TargetId,
            createInput.OnDown,
            createInput.OnUp,
            createInput.MinDownDurationSeconds,
            createInput.CooldownSeconds,
            emailString,
            createInput.WebhookUrl);
    }

    protected override async Task MapToEntityAsync(CreateUpdateAlertPolicyDto updateInput, AlertPolicy entity)
    {
        await ValidateDtoAsync(updateInput);
        var emails = NormalizeEmails(updateInput.Emails);
        var emailString = string.Join(";", emails);

        entity.SetTargetId(updateInput.TargetId);
        entity.Update(
            updateInput.OnDown,
            updateInput.OnUp,
            updateInput.MinDownDurationSeconds,
            updateInput.CooldownSeconds,
            emailString,
            updateInput.WebhookUrl);
    }

    protected override AlertPolicyDto MapToGetOutputDto(AlertPolicy entity)
    {
        var dto = base.MapToGetOutputDto(entity);
        dto.Emails = ParseEmailString(entity.Emails).ToArray();
        return dto;
    }

    protected override AlertPolicyDto MapToGetListOutputDto(AlertPolicy entity)
    {
        var dto = base.MapToGetListOutputDto(entity);
        dto.Emails = ParseEmailString(entity.Emails).ToArray();
        return dto;
    }

    private async Task ValidateDtoAsync(CreateUpdateAlertPolicyDto dto)
    {
        await Task.CompletedTask;

        if (dto.MinDownDurationSeconds < 0)
        {
            throw new BusinessException("Monitoring:InvalidMinDownDuration");
        }

        if (dto.CooldownSeconds < 0)
        {
            throw new BusinessException("Monitoring:InvalidCooldown");
        }

        if (!string.IsNullOrWhiteSpace(dto.WebhookUrl))
        {
            if (!Uri.TryCreate(dto.WebhookUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new BusinessException("Monitoring:InvalidWebhookUrl");
            }
        }

        foreach (var email in NormalizeEmails(dto.Emails))
        {
            try
            {
                _ = new MailAddress(email);
            }
            catch (FormatException)
            {
                throw new BusinessException("Monitoring:InvalidEmailAddress").WithData("Email", email);
            }
        }
    }

    private static List<string> NormalizeEmails(IEnumerable<string> emails)
    {
        return emails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Where(e => e.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> ParseEmailString(string emails)
    {
        if (string.IsNullOrWhiteSpace(emails))
        {
            return Array.Empty<string>();
        }

        return emails
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => e.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
