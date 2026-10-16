param([Parameter(Mandatory=$true)][string]$TracePath)
$ErrorActionPreference = 'Stop'
$npcTraceFile = Get-Item -LiteralPath $TracePath
if ($npcTraceFile.PSIsContainer) { throw 'TracePath must be one jsonl file.' }
$npcRows = @(Get-Content -LiteralPath $npcTraceFile.FullName | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { ConvertFrom-Json -InputObject $_ })
if ($npcRows.Count -eq 0) { throw 'No trace entries.' }
if (@($npcRows | Where-Object { $_.schema -ne 1 }).Count -gt 0) { throw 'Unsupported trace schema.' }
foreach ($npcGroup in ($npcRows | Group-Object session)) {
    $npcEvents = @($npcGroup.Group)
    $npcReplies = @($npcEvents | Where-Object { $_.kind -eq 'reply' })
    $npcLatency = @($npcReplies | ForEach-Object { [double]$_.duration } | Sort-Object)
    $npcFirstInvite = $npcEvents | Where-Object { $_.kind -eq 'invitation' -and $_.outcome -eq 'shown' } | Select-Object -First 1
    $npcFirstReport = $npcEvents | Where-Object { $_.kind -eq 'report' } | Select-Object -First 1
    $npcMedian = $null; $npcP95 = $null
    if ($npcLatency.Count -gt 0) {
        $npcMiddle = [int][math]::Floor($npcLatency.Count / 2)
        $npcMedian = if ($npcLatency.Count % 2 -eq 0) { ($npcLatency[$npcMiddle - 1] + $npcLatency[$npcMiddle]) / 2 } else { $npcLatency[$npcMiddle] }
        $npcP95 = $npcLatency[[math]::Max(0, [int][math]::Ceiling($npcLatency.Count * 0.95) - 1)]
    }
    [pscustomobject]@{
        Session = $npcGroup.Name
        Build = $npcEvents[0].build
        Requests = @($npcEvents | Where-Object { $_.kind -eq 'request' }).Count
        Replies = $npcReplies.Count
        FallbackReplies = @($npcReplies | Where-Object { $_.route -eq 'fallback' }).Count
        ClarificationsAsked = @($npcEvents | Where-Object { $_.kind -eq 'clarification' }).Count
        ClarificationsMatched = @($npcEvents | Where-Object { $_.kind -eq 'clarification_resolved' }).Count
        CompletedTasks = @($npcEvents | Where-Object { $_.kind -eq 'action_end' -and $_.outcome -eq 'completed' }).Count
        CancelledTasks = @($npcEvents | Where-Object { $_.kind -eq 'action_end' -and $_.outcome -eq 'cancelled' }).Count
        FailedTasks = @($npcEvents | Where-Object { $_.kind -eq 'action_end' -and $_.outcome -eq 'failed' }).Count
        FactsReported = @($npcEvents | Where-Object { $_.kind -eq 'report' }).Count
        Shares = @($npcEvents | Where-Object { $_.kind -eq 'share' -and $_.outcome -eq 'shared' }).Count
        Hypotheses = @($npcEvents | Where-Object { $_.kind -eq 'hypothesis' }).Count
        FirstInvitationSeconds = if ($null -eq $npcFirstInvite) { $null } else { $npcFirstInvite.seconds }
        FirstReportSeconds = if ($null -eq $npcFirstReport) { $null } else { $npcFirstReport.seconds }
        ReplyLatencyMedianSeconds = $npcMedian
        ReplyLatencyP95Seconds = $npcP95
        SessionClosed = @($npcEvents | Where-Object { $_.kind -eq 'session_end' }).Count -gt 0
    }
}
