#requires -Version 5.1
<#
.SYNOPSIS
    Run one BMAD story through opencode, verify the repository gates, and open a PR.

.DESCRIPTION
    This is the only supported unattended coding entry point. It refuses to run on a
    dirty tree, creates or switches to story/<id>, invokes the opencode development
    agent with the story prompt from stories.yaml, runs build/test/format gates,
    retries at most -MaxAttempts times, commits, pushes, and opens a PR. It never
    merges and never edits SPEC.md, architecture.md, release workflows, story
    metadata, or Directory.Packages.props.

.EXAMPLE
    .\scripts\auto-build.ps1 -Story STORY-001
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^STORY-\d{3}$')]
    [string]$Story,

    [ValidateRange(1, 3)]
    [int]$MaxAttempts = 3,

    [string]$Model,
    [string]$Agent
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repoRoot
$storiesPath = Join-Path $repoRoot '_bmad-output\planning-artifacts\specs\spec-microslopwords\stories.yaml'
$artifactsDir = Join-Path $repoRoot '_bmad-output\implementation-artifacts'
$deferredPath = Join-Path $artifactsDir 'deferred-work.md'
$logDir = Join-Path $artifactsDir 'logs'
$branch = "story/$Story"

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$Arguments = @(),
        [string]$LogPath,
        [switch]$Append
    )

    $output = & $FilePath @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $text = @($output | ForEach-Object { "$_" })

    if ($LogPath) {
        $parent = Split-Path -Parent $LogPath
        if ($parent -and -not (Test-Path -LiteralPath $parent)) {
            New-Item -ItemType Directory -Path $parent -Force | Out-Null
        }
        $text | Out-File -LiteralPath $LogPath -Encoding utf8 -Append:$Append
    }

    if ($text.Count -gt 0) {
        $text | ForEach-Object { Write-Host $_ }
    }

    return $exitCode
}

function Get-StoryField {
    param(
        [Parameter(Mandatory = $true)][string]$Yaml,
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][string]$Field
    )

    $blocks = [regex]::Split($Yaml, '(?m)^- id:\s*') | Where-Object { $_ -ne '' }
    $block = $blocks | Where-Object { $_ -match ('^"?' + [regex]::Escape($Id) + '"?\s*(\r?\n|$)') } | Select-Object -First 1
    if (-not $block) {
        throw "Story '$Id' was not found in stories.yaml."
    }

    if ($Field -eq 'title') {
        $match = [regex]::Match($block, '(?m)^\s*title:\s*(.+?)\s*$')
        if (-not $match.Success) {
            throw "Story '$Id' has no title."
        }
        return $match.Groups[1].Value.Trim('"', "'")
    }

    if ($Field -eq 'invoke_dev_with') {
        $match = [regex]::Match($block, '(?ms)^\s*invoke_dev_with:\s*>-?\s*\r?\n(?<value>(?:[ \t]+.+(?:\r?\n|$))+)')
        if (-not $match.Success) {
            throw "Story '$Id' has no invoke_dev_with block."
        }
        $lines = $match.Groups['value'].Value -split "\r?\n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
        return ($lines -join ' ')
    }

    throw "Unsupported story field '$Field'."
}

function Get-ChangedFiles {
    $files = @()
    $files += @(git -C $repoRoot diff --name-only)
    $files += @(git -C $repoRoot diff --cached --name-only)
    $files += @(git -C $repoRoot ls-files --others --exclude-standard)
    return @($files | Where-Object { $_ -ne '' } | Sort-Object -Unique)
}

function Assert-ProtectedFilesUntouched {
    param([string[]]$Files)

    $protected = @(
        '^_bmad-output/planning-artifacts/specs/.*/SPEC\.md$',
        '^_bmad-output/planning-artifacts/specs/.*/stories\.yaml$',
        '^_bmad-output/planning-artifacts/architecture\.md$',
        '^\.github/workflows/',
        '^Directory\.Packages\.props$'
    )

    $violations = @()
    foreach ($file in $Files) {
        $normalized = $file -replace '\\', '/'
        foreach ($pattern in $protected) {
            if ($normalized -match $pattern) {
                $violations += $normalized
                break
            }
        }
    }

    if ($violations.Count -gt 0) {
        throw "Protected files changed without human review: $($violations -join ', ')."
    }
}

function Assert-NoSecrets {
    $diff = @(git -C $repoRoot diff --cached --no-color --unified=0)
    $text = $diff -join "`n"
    $patterns = @(
        'ghp_[A-Za-z0-9]{20,}',
        'github_pat_[A-Za-z0-9_]{20,}',
        'sk-[A-Za-z0-9]{20,}',
        'xox[baprs]-[A-Za-z0-9-]{10,}',
        '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----',
        'https://discord\.com/api/webhooks/'
    )

    foreach ($pattern in $patterns) {
        if ($text -match $pattern) {
            throw "Potential secret detected in staged changes. Refusing to commit."
        }
    }
}

function Add-DeferredFailure {
    param(
        [Parameter(Mandatory = $true)][string]$Reason,
        [string]$LogPath
    )

    if (-not (Test-Path -LiteralPath $deferredPath)) {
        Set-Content -LiteralPath $deferredPath -Encoding utf8 -Value @(
            '# Deferred Work',
            '',
            'The build harness appends failures here and stops instead of inventing scope.',
            ''
        )
    }

    $tail = ''
    if ($LogPath -and (Test-Path -LiteralPath $LogPath)) {
        $tail = (Get-Content -LiteralPath $LogPath -Tail 60) -join "`n"
    }

    $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    Add-Content -LiteralPath $deferredPath -Encoding utf8 -Value @(
        '',
        "## $Story - $stamp",
        '',
        "Automation stopped after $MaxAttempts attempt(s).",
        '',
        "**Reason:** $Reason",
        '',
        "**Log:** ``$LogPath``",
        '',
        '<details><summary>Last output</summary>',
        '',
        '````text',
        $tail,
        '````',
        '',
        '</details>',
        ''
    )
}

function Invoke-Gate {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$LogPath
    )

    Write-Host "Running gate: $Name"
    $code = Invoke-Native -FilePath 'dotnet' -Arguments $Arguments -LogPath $LogPath -Append
    if ($code -ne 0) {
        throw "Gate '$Name' failed with exit code $code."
    }
}

$lastLog = ''

try {
    if (-not (Test-Path -LiteralPath $storiesPath)) {
        throw "Stories file not found: $storiesPath"
    }
    if (-not (Get-Command opencode -ErrorAction SilentlyContinue)) {
        throw 'opencode was not found on PATH.'
    }
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw 'GitHub CLI (gh) was not found on PATH.'
    }

    $dirty = @(git -C $repoRoot status --porcelain)
    if ($dirty.Count -gt 0) {
        throw 'Working tree is dirty. Commit or stash changes before running the harness.'
    }

    $currentBranch = (git -C $repoRoot branch --show-current).Trim()
    if (-not $currentBranch) {
        throw 'Could not determine the current branch. The repository needs an initial commit.'
    }

    if ($currentBranch -eq 'main') {
        Write-Host "Creating $branch from main."
        git -C $repoRoot switch -c $branch
        if ($LASTEXITCODE -ne 0) { throw "Could not create branch $branch." }
    }
    elseif ($currentBranch -ne $branch) {
        $exists = git -C $repoRoot show-ref --verify --quiet "refs/heads/$branch"
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Switching to existing branch $branch."
            git -C $repoRoot switch $branch
        }
        else {
            Write-Host "Creating $branch from $currentBranch."
            git -C $repoRoot switch -c $branch
        }
        if ($LASTEXITCODE -ne 0) { throw "Could not switch to branch $branch." }
    }

    $activeBranch = (git -C $repoRoot branch --show-current).Trim()
    if ($activeBranch -eq 'main' -or $activeBranch -ne $branch) {
        throw "Refusing to work on branch '$activeBranch'; expected '$branch'."
    }

    $yaml = Get-Content -LiteralPath $storiesPath -Raw
    $title = Get-StoryField -Yaml $yaml -Id $Story -Field 'title'
    $invoke = Get-StoryField -Yaml $yaml -Id $Story -Field 'invoke_dev_with'

    $prompt = @"
You are the BMAD development agent implementing exactly one story in the MicroslopWords repository.

Story: $Story - $title

Read AGENTS.md, the full story section in _bmad-output/planning-artifacts/epics.md, the referenced SPEC capabilities, and the architecture/UX constraints before editing code.
Implement only this story. Add or update focused tests. Follow the existing architecture and dependency direction.
Do not edit SPEC.md, architecture.md, stories.yaml, release workflows, or Directory.Packages.props.
Do not commit; the harness commits after the gates pass.
If a SPEC checkpoint applies and the spec is ambiguous, stop and report the question instead of inventing scope.

$invoke
"@

    $attempt = 1
    $succeeded = $false
    $lastFailure = ''
    $lastLog = ''

    while ($attempt -le $MaxAttempts) {
        $logPath = Join-Path $logDir "$Story-attempt-$attempt.log"
        $lastLog = $logPath
        Write-Host "Attempt $attempt of $MaxAttempts for $Story."

        $agentArgs = @('run', '--auto', '--dir', $repoRoot, '--title', "$Story attempt $attempt")
        if ($Model) { $agentArgs += @('--model', $Model) }
        if ($Agent) { $agentArgs += @('--agent', $Agent) }
        $agentArgs += $prompt

        $agentExit = Invoke-Native -FilePath 'opencode' -Arguments $agentArgs -LogPath $logPath
        if ($agentExit -ne 0) {
            $lastFailure = "Agent exited with code $agentExit."
            Write-Warning $lastFailure
            $attempt++
            continue
        }

        $changed = Get-ChangedFiles
        if ($changed.Count -eq 0) {
            $lastFailure = 'Agent produced no file changes.'
            Write-Warning $lastFailure
            $attempt++
            continue
        }

        try {
            Assert-ProtectedFilesUntouched -Files $changed
            Invoke-Gate -Name 'build' -Arguments @('build', 'MicroslopWords.slnx') -LogPath $logPath
            Invoke-Gate -Name 'test' -Arguments @('test', 'MicroslopWords.slnx', '--no-build') -LogPath $logPath
            Invoke-Gate -Name 'format' -Arguments @('format', '--verify-no-changes') -LogPath $logPath

            $diffCheck = Invoke-Native -FilePath 'git' -Arguments @('-C', $repoRoot, 'diff', '--check') -LogPath $logPath -Append
            if ($diffCheck -ne 0) {
                throw 'git diff --check found whitespace errors.'
            }

            $succeeded = $true
            break
        }
        catch {
            $lastFailure = $_.Exception.Message
            Write-Warning $lastFailure
            $prompt = @"
The previous attempt at $Story failed verification: $lastFailure

Inspect the current working tree, fix the failure, and keep the implementation within the story scope.
Do not edit SPEC.md, architecture.md, stories.yaml, release workflows, or Directory.Packages.props.
Do not commit; the harness commits after the gates pass.

$invoke
"@
            $attempt++
        }
    }

    if (-not $succeeded) {
        Add-DeferredFailure -Reason $lastFailure -LogPath $lastLog
        throw "Story $Story failed after $MaxAttempts attempts. See deferred-work.md."
    }

    $changed = Get-ChangedFiles
    Assert-ProtectedFilesUntouched -Files $changed
    git -C $repoRoot add -A
    if ($LASTEXITCODE -ne 0) { throw 'git add failed.' }
    Assert-NoSecrets

    $commitMessage = "${Story}: $title"
    git -C $repoRoot commit -m $commitMessage
    if ($LASTEXITCODE -ne 0) { throw 'git commit failed.' }

    git -C $repoRoot push -u origin $branch
    if ($LASTEXITCODE -ne 0) { throw "git push failed for $branch." }

    $prUrl = gh pr view $branch --json url --jq '.url' 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $prUrl) {
        $prBody = @'
## Story

{0} - {1}

## Summary

Automated implementation by `scripts/auto-build.ps1`.

## Gates

- `dotnet build MicroslopWords.slnx`
- `dotnet test MicroslopWords.slnx --no-build`
- `dotnet format --verify-no-changes`

## Review notes

- Human review is required before merging.
- SPEC and architecture checkpoints still apply where the story requires them.
'@ -f $Story, $title
        $prUrl = gh pr create --base main --head $branch --title $commitMessage --body $prBody
        if ($LASTEXITCODE -ne 0) { throw 'gh pr create failed.' }
    }

    Write-Host "Story $Story is ready for review: $prUrl"
    exit 0
}
catch {
    $message = $_.Exception.Message
    if ($message -notmatch 'failed after') {
        Add-DeferredFailure -Reason $message -LogPath $lastLog
    }
    Write-Error $message
    exit 1
}
finally {
    Pop-Location
}
