#Requires -Version 7.0
<#
.SYNOPSIS
  One-click register a GitHub App (manifest) for BooksMobile and print the public client_id.
#>
param(
    [int]$Port = 3847,
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'
$state = [guid]::NewGuid().ToString('N')
$redirect = "http://127.0.0.1:$Port/callback"
$manifest = @{
    name        = 'Novolis BooksMobile'
    url         = 'https://github.com/frankhaugen/books'
    hook_attributes = @{ url = 'https://example.com/webhook' }
    redirect_url = $redirect
    callback_urls = @($redirect)
    description = 'Books markdown editor (Android + Windows) for frankhaugen/books'
    public      = $false
    default_permissions = @{
        contents = 'write'
        metadata = 'read'
    }
    request_oauth_on_install = $true
} | ConvertTo-Json -Compress -Depth 5

$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
$listener.Start()

$formHtml = @"
<!DOCTYPE html>
<html><body>
<form id="f" action="https://github.com/settings/apps/new?state=$state" method="post">
<input type="hidden" name="manifest" value="$([System.Net.WebUtility]::HtmlEncode($manifest))">
</form>
<script>document.getElementById('f').submit();</script>
<p>Redirecting to GitHub to create Novolis BooksMobile…</p>
</body></html>
"@

$formPath = Join-Path $env:TEMP "booksmobile-github-app-manifest.html"
Set-Content -Path $formPath -Value $formHtml -Encoding UTF8
Start-Process $formPath

Write-Host "Waiting for GitHub App creation (approve in browser, passkey OK)…"
Write-Host "Timeout: ${TimeoutSeconds}s"

$code = $null
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while ([DateTime]::UtcNow -lt $deadline -and -not $code) {
    $ctxTask = $listener.GetContextAsync()
    $remain = [int][Math]::Max(1000, ($deadline - [DateTime]::UtcNow).TotalMilliseconds)
    if (-not $ctxTask.Wait($remain)) { break }
    $ctx = $ctxTask.Result
    $req = $ctx.Request
    if ($req.Url.AbsolutePath -eq '/callback') {
        $code = $req.QueryString['code']
        $gotState = $req.QueryString['state']
        $body = if ($code -and $gotState -eq $state) {
            '<html><body><h1>BooksMobile GitHub App created</h1><p>You can close this tab.</p></body></html>'
        } else {
            '<html><body><h1>Missing code</h1></body></html>'
        }
        $bytes = [Text.Encoding]::UTF8.GetBytes($body)
        $ctx.Response.ContentType = 'text/html'
        $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
        $ctx.Response.Close()
        if (-not $code -or $gotState -ne $state) { $code = $null }
    } else {
        $ctx.Response.StatusCode = 404
        $ctx.Response.Close()
    }
}

$listener.Stop()
if (-not $code) { throw 'Timed out waiting for GitHub App creation callback.' }

Write-Host "Exchanging manifest code…"
$conv = gh api -X POST "app-manifests/$code/conversions" | ConvertFrom-Json
if (-not $conv.client_id) { throw "Conversion failed: $($conv | ConvertTo-Json -Compress)" }

$result = [pscustomobject]@{
    ClientId     = $conv.client_id
    Slug         = $conv.slug
    HtmlUrl      = $conv.html_url
    Name         = $conv.name
}
$result | ConvertTo-Json
$out = Join-Path $env:LOCALAPPDATA 'Novolis\BooksMobile\github-app.json'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
$result | ConvertTo-Json | Set-Content $out -Encoding UTF8
Set-Content (Join-Path (Split-Path $out) 'github-client-id.txt') $result.ClientId -NoNewline
Write-Host "Client ID: $($result.ClientId)"
Write-Host "Install the app on frankhaugen/books: $($result.HtmlUrl)/installations/new"
