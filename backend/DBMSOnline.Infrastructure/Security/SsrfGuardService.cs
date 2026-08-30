using System;
using System.Net;
using System.Net.Sockets;
using DBMSOnline.Domain.Exceptions;
using DBMSOnline.Domain.Interfaces;

namespace DBMSOnline.Infrastructure.Security;

public class SsrfGuardService : ISsrfGuard
{
    private readonly bool _allowLocalhost;

    public SsrfGuardService(bool allowLocalhost = true)
    {
        _allowLocalhost = allowLocalhost;
    }

    public void ValidateHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return;

        host = host.Trim();

        // Extract clean hostname/IP if full URL or connection string is passed
        string targetHost = host;

        if (targetHost.Contains("://"))
        {
            try
            {
                var uri = new Uri(targetHost);
                targetHost = uri.Host;
            }
            catch
            {
                var match = System.Text.RegularExpressions.Regex.Match(targetHost, @"@([^/:\?]+)");
                if (match.Success)
                {
                    targetHost = match.Groups[1].Value;
                }
            }
        }
        else if (targetHost.Contains("Server=") || targetHost.Contains("Data Source=") || targetHost.Contains("Host="))
        {
            var match = System.Text.RegularExpressions.Regex.Match(targetHost, @"(?:Server|Data Source|Host)=([^;]+)");
            if (match.Success)
            {
                targetHost = match.Groups[1].Value;
            }
        }

        if (targetHost.Contains('/'))
        {
            targetHost = targetHost.Split('/')[0];
        }
        if (targetHost.Contains('\\'))
        {
            targetHost = targetHost.Split('\\')[0];
        }
        if (targetHost.Contains(','))
        {
            targetHost = targetHost.Split(',')[0];
        }
        if (targetHost.Contains(':') && !targetHost.Contains("["))
        {
            targetHost = targetHost.Split(':')[0];
        }
        targetHost = targetHost.Trim();

        if (string.IsNullOrWhiteSpace(targetHost) ||
            targetHost.Equals(".", StringComparison.OrdinalIgnoreCase) || 
            targetHost.Equals("(local)", StringComparison.OrdinalIgnoreCase))
        {
            targetHost = "127.0.0.1";
        }

        // 1. Block Cloud Metadata Endpoints
        if (targetHost.Equals("169.254.169.254", StringComparison.OrdinalIgnoreCase) ||
            targetHost.Equals("metadata.google.internal", StringComparison.OrdinalIgnoreCase) ||
            targetHost.Equals("169.254.169.253", StringComparison.OrdinalIgnoreCase))
        {
            throw new SsrfBlockedException(host, "Cloud Metadata endpoint access is strictly forbidden.");
        }

        // 2. Resolve Host IP Addresses
        try
        {
            IPAddress[] addresses;
            if (IPAddress.TryParse(targetHost, out var parsedIp))
            {
                addresses = new[] { parsedIp };
            }
            else
            {
                addresses = Dns.GetHostAddresses(targetHost);
            }

            foreach (var ip in addresses)
            {
                // Check Loopback
                if (IPAddress.IsLoopback(ip))
                {
                    if (!_allowLocalhost)
                        throw new SsrfBlockedException(host, "Loopback / Localhost addresses are restricted.");
                }

                // Check Link Local
                if (ip.IsIPv6LinkLocal || (ip.AddressFamily == AddressFamily.InterNetwork && ip.ToString().StartsWith("169.254.")))
                {
                    if (!_allowLocalhost)
                        throw new SsrfBlockedException(host, "Link-local IP address range is forbidden.");
                }
            }
        }
        catch (SsrfBlockedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (targetHost.StartsWith("dpg-", StringComparison.OrdinalIgnoreCase) && !targetHost.Contains('.'))
            {
                throw new SsrfBlockedException(host, $"'{host}' is an internal Render hostname. To connect remotely, please copy the 'External Database URL' or external hostname ending in '.render.com' from your Render dashboard, and check 'Enforce TLS/SSL'.");
            }
            throw new SsrfBlockedException(host, $"Failed to resolve host for security validation: {ex.Message}");
        }
    }
}
