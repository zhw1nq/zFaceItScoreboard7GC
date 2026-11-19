# ==============================================================================
# FaceIT CS2 Challenger & Banned Checker Test Tool
# Plugin: zFaceItScoreboard7GC
# ==============================================================================

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$keyFilePath = Join-Path $scriptDir "faceit_api_key.txt"

function Write-Header {
    Clear-Host
    Write-Host "=================================================================" -ForegroundColor Cyan
    Write-Host "   FACEIT CS2 RANK & CHALLENGER CHECKER TEST TOOL (OFFICIAL API)  " -ForegroundColor Yellow
    Write-Host "   Plugin: zFaceItScoreboard7GC - SwiftlyS2                      " -ForegroundColor DarkGray
    Write-Host "=================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Get-ApiKey {
    if (Test-Path $keyFilePath) {
        $savedKey = (Get-Content $keyFilePath -Raw).Trim()
        if (-not [string]::IsNullOrWhiteSpace($savedKey)) {
            Write-Host "[INFO] Đang sử dụng FaceIT API Key lưu tại: faceit_api_key.txt" -ForegroundColor Green
            Write-Host "       (Để đổi key, xóa file 'faceit_api_key.txt' hoặc chỉnh sửa file đó)" -ForegroundColor DarkGray
            Write-Host ""
            return $savedKey
        }
    }

    Write-Host "Chưa tìm thấy FaceIT API Key." -ForegroundColor Yellow
    Write-Host "Vui lòng lấy API Key tại: https://developers.faceit.com/" -ForegroundColor Cyan
    Write-Host ""
    $apiKey = (Read-Host "Nhập FaceIT API Key (Bearer Token)").Trim()

    while ([string]::IsNullOrWhiteSpace($apiKey)) {
        Write-Host "API Key không được để trống!" -ForegroundColor Red
        $apiKey = (Read-Host "Nhập lại FaceIT API Key").Trim()
    }

    $save = Read-Host "Bạn có muốn lưu API Key này vào 'faceit_api_key.txt' để lần sau không cần nhập lại? (Y/N)"
    if ($save -match "^[yY]") {
        Set-Content -Path $keyFilePath -Value $apiKey -Encoding UTF8
        Write-Host "[OK] Đã lưu API Key vào faceit_api_key.txt" -ForegroundColor Green
    }
    Write-Host ""
    return $apiKey
}

function Resolve-SteamInput {
    param([string]$inputStr)

    $trimmed = $inputStr.Trim()

    # 1. Check if raw SteamID64 (17 digits starting with 765611)
    if ($trimmed -match '^\d{17}$') {
        return @{ Type = "SteamID"; Value = $trimmed }
    }

    # 2. Check if Steam profile link: https://steamcommunity.com/profiles/76561198...
    if ($trimmed -match 'steamcommunity\.com\/profiles\/(\d{17})') {
        return @{ Type = "SteamID"; Value = $Matches[1] }
    }

    # 3. Check if Steam custom vanity URL: https://steamcommunity.com/id/vanity_name
    if ($trimmed -match 'steamcommunity\.com\/id\/([^\/\?]+)') {
        $vanity = $Matches[1]
        Write-Host "[RESOLVE] Đang chuyển đổi Custom URL '$vanity' sang SteamID64 qua Steam XML..." -ForegroundColor Gray
        try {
            [xml]$xml = Invoke-RestMethod -Uri "https://steamcommunity.com/id/$vanity/?xml=1" -TimeoutSec 5 -ErrorAction Stop
            if ($xml.profile.steamID64) {
                $resolvedId = $xml.profile.steamID64.Trim()
                Write-Host "[RESOLVE] Đã tìm thấy SteamID64: $resolvedId" -ForegroundColor Green
                return @{ Type = "SteamID"; Value = $resolvedId }
            }
        }
        catch {
            Write-Host "[WARN] Không thể lấy SteamID qua Steam XML: $($_.Exception.Message)" -ForegroundColor DarkYellow
            Write-Host "       Thử tìm kiếm với Nickname '$vanity' trên FaceIT..." -ForegroundColor DarkYellow
            return @{ Type = "Nickname"; Value = $vanity }
        }
    }

    # 4. Otherwise treat as FaceIT Nickname
    return @{ Type = "Nickname"; Value = $trimmed }
}

function Test-FaceitPlayer {
    param(
        [string]$apiKey,
        [hashtable]$target
    )

    $headers = @{
        "Authorization" = "Bearer $apiKey"
        "Accept"        = "application/json"
    }

    # 1. Fetch Player Details
    if ($target.Type -eq "SteamID") {
        $url = "https://open.faceit.com/data/v4/players?game=cs2&game_player_id=$($target.Value)"
        Write-Host "[API 1/3] Đang gọi: GET /data/v4/players?game=cs2&game_player_id=$($target.Value)" -ForegroundColor Gray
    } else {
        $url = "https://open.faceit.com/data/v4/players?nickname=$($target.Value)"
        Write-Host "[API 1/3] Đang gọi: GET /data/v4/players?nickname=$($target.Value)" -ForegroundColor Gray
    }

    try {
        $player = Invoke-RestMethod -Uri $url -Headers $headers -Method Get -TimeoutSec 10 -ErrorAction Stop
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 404) {
            Write-Host ""
            Write-Host "[KET QUA] KHONG TIM THAY TAI KHOAN FACEIT (NULL / UNRANKED)" -ForegroundColor Yellow
            Write-Host "Mo ta: Khong co tai khoan FaceIT lien ket voi $($target.Type): $($target.Value)" -ForegroundColor DarkGray
            Write-Host "Scoreboard Coin tuong ung: 5138 (NullCoinId)" -ForegroundColor DarkGray
            return
        } elseif ($statusCode -eq 401) {
            Write-Host ""
            Write-Host "[LOI] API KEY KHONG HOP LE HOAC HET HAN (HTTP 401 Unauthorized)" -ForegroundColor Red
            Write-Host "Vui long kiem tra lai FaceIT API Key trong file 'faceit_api_key.txt'." -ForegroundColor Red
            return
        } else {
            Write-Host ""
            Write-Host "[LOI] KHI GOI FACEIT API: $($_.Exception.Message)" -ForegroundColor Red
            return
        }
    }

    $playerId = $player.player_id
    $nickname = $player.nickname
    $status = $player.status
    $steamId64 = $player.games.cs2.game_player_id
    if (-not $steamId64) {
        $steamId64 = $player.steam_id_64
    }

    Write-Host ""
    Write-Host "---------------- THÔNG TIN TÀI KHOẢN FACEIT ----------------" -ForegroundColor Cyan
    $displaySteamId = if ($steamId64) { $steamId64 } else { "N/A" }
    $statusColor = if ($status -eq "active") { "Green" } else { "Red" }
    Write-Host "Nickname  : " -NoNewline; Write-Host $nickname -ForegroundColor White
    Write-Host "Player ID : " -NoNewline; Write-Host $playerId -ForegroundColor DarkGray
    Write-Host "SteamID64 : " -NoNewline; Write-Host $displaySteamId -ForegroundColor White
    Write-Host "Country   : " -NoNewline; Write-Host $player.country -ForegroundColor White
    Write-Host "Status    : " -NoNewline; Write-Host $status -ForegroundColor $statusColor

    # 2. Check Bans
    $isBanned = ($status -eq "banned")
    $banReason = ""
    try {
        $bansUrl = "https://open.faceit.com/data/v4/players/$playerId/bans"
        Write-Host "[API 2/3] Đang gọi: GET /data/v4/players/{player_id}/bans" -ForegroundColor Gray
        $bansResult = Invoke-RestMethod -Uri $bansUrl -Headers $headers -Method Get -TimeoutSec 8 -ErrorAction Stop

        if ($bansResult.items -and $bansResult.items.Count -gt 0) {
            $currentTimestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
            foreach ($ban in $bansResult.items) {
                # ends_at == 0 means permanent ban
                if ($ban.ends_at -eq 0 -or $ban.ends_at -gt $currentTimestamp) {
                    $isBanned = $true
                    $banReason = $ban.reason
                    break
                }
            }
        }
    }
    catch {
        Write-Host "[WARN] Không thể kiểm tra bans endpoint: $($_.Exception.Message)" -ForegroundColor DarkYellow
    }

    if ($isBanned) {
        Write-Host ""
        Write-Host "=============================================================" -ForegroundColor Red
        Write-Host "[KET QUA] BANNED TREN FACEIT!" -ForegroundColor Red
        if ($banReason) {
            Write-Host "Ly do Ban : $banReason" -ForegroundColor Yellow
        }
        Write-Host "Coin ID tren Scoreboard: 1018 (BannedCoinId - Uu tien cao nhat)" -ForegroundColor White
        Write-Host "=============================================================" -ForegroundColor Red
        return
    }

    # 3. Check CS2 Game Data
    $cs2 = $player.games.cs2
    if (-not $cs2) {
        Write-Host ""
        Write-Host "[KET QUA] CHUA CO DU LIEU CS2 TREN FACEIT" -ForegroundColor Yellow
        Write-Host "Nguoi choi co tai khoan FaceIT nhung chua kich hoat game CS2." -ForegroundColor DarkGray
        Write-Host "Coin ID tren Scoreboard: 5138 (NullCoinId)" -ForegroundColor DarkGray
        return
    }

    $skillLevel = [int]$cs2.skill_level
    $faceitElo = [int]$cs2.faceit_elo
    $region = [string]$cs2.region

    $levelColor = if ($skillLevel -eq 10) { "Magenta" } else { "Green" }
    Write-Host "Game      : " -NoNewline; Write-Host "Counter-Strike 2 (CS2)" -ForegroundColor Green
    Write-Host "Region    : " -NoNewline; Write-Host $region -ForegroundColor Yellow
    Write-Host "SkillLevel: " -NoNewline; Write-Host $skillLevel -ForegroundColor $levelColor
    Write-Host "FaceIT Elo: " -NoNewline; Write-Host $faceitElo -ForegroundColor White

    # 4. Check Challenger logic (Only for Skill Level 10)
    $isChallenger = $false
    $regionalRank = $null

    if ($skillLevel -ge 10 -and -not [string]::IsNullOrWhiteSpace($region)) {
        try {
            $rankingUrl = "https://open.faceit.com/data/v4/rankings/games/cs2/regions/$region/players/$playerId"
            Write-Host "[API 3/3] Dang goi: GET /data/v4/rankings/games/cs2/regions/$region/players/{player_id}" -ForegroundColor Gray
            $ranking = Invoke-RestMethod -Uri $rankingUrl -Headers $headers -Method Get -TimeoutSec 8 -ErrorAction Stop

            if ($ranking.position) {
                $regionalRank = [int]$ranking.position
                Write-Host "Thu hang khu vuc ($region): #$regionalRank" -ForegroundColor Cyan

                if ($regionalRank -le 1000 -and $regionalRank -gt 0) {
                    $isChallenger = $true
                }
            }
        }
        catch {
            Write-Host "[WARN] Khong the lay Ranking khu vuc: $($_.Exception.Message)" -ForegroundColor DarkYellow
        }
    } else {
        Write-Host "[INFO] Skill Level ($skillLevel) < 10 -> Khong thuoc dien kiem tra Challenger." -ForegroundColor DarkGray
    }

    # 5. Display Final Verdict
    Write-Host ""
    Write-Host "====================== KET QUA PHAN LOAI ======================" -ForegroundColor Cyan
    if ($isChallenger) {
        Write-Host "[KET QUA] DAY LA CHALLENGER!" -ForegroundColor Green -BackgroundColor Black
        Write-Host "Chi tiet  : Nam trong TOP 1000 khu vuc $region (Hang #$regionalRank)" -ForegroundColor Yellow
        Write-Host "Coin ID   : 1089 (ChallengerCoinId)" -ForegroundColor Green
    }
    elseif ($skillLevel -eq 10) {
        Write-Host "[KET QUA] DAY LA LEVEL 10 (KHONG PHAI CHALLENGER)" -ForegroundColor Magenta
        if ($regionalRank) {
            Write-Host "Chi tiet  : Dat Level 10 nhung xep hang #$regionalRank trong khu vuc $region (> 1000)" -ForegroundColor Gray
        } else {
            Write-Host "Chi tiet  : Dat Level 10 nhung chua co thu hang top trong khu vuc." -ForegroundColor Gray
        }
        Write-Host "Coin ID   : 1017 (Level 10 Coin)" -ForegroundColor White
    }
    else {
        Write-Host "[KET QUA] DAY LA LEVEL $skillLevel (KHONG PHAI CHALLENGER)" -ForegroundColor White
        Write-Host "FaceIT Elo: $faceitElo" -ForegroundColor Gray
        $levelCoins = @{
            1 = 1061; 2 = 1032; 3 = 1041; 4 = 1074; 5 = 1067;
            6 = 1088; 7 = 1039; 8 = 1055; 9 = 1087; 10 = 1017
        }
        $coinId = $levelCoins[$skillLevel]
        Write-Host "Coin ID   : $coinId (Level $skillLevel Coin)" -ForegroundColor White
    }
    Write-Host "===============================================================" -ForegroundColor Cyan
}

# ----------------- MAIN PROGRAM LOOP -----------------
function Start-FaceitTester {
    Write-Header
    $apiKey = Get-ApiKey

    while ($true) {
        Write-Host ""
        Write-Host "Nhập một trong các định dạng sau:" -ForegroundColor Cyan
        Write-Host "  1. Link Steam Profile : https://steamcommunity.com/profiles/76561198000000000" -ForegroundColor DarkGray
        Write-Host "  2. Link Steam Custom  : https://steamcommunity.com/id/custom_name" -ForegroundColor DarkGray
        Write-Host "  3. SteamID64          : 76561198000000000" -ForegroundColor DarkGray
        Write-Host "  4. FaceIT Nickname    : s1mple, m0NESY, ..." -ForegroundColor DarkGray
        Write-Host "  (Gõ 'exit' hoặc 'q' để thoát, 'clear' để làm mới màn hình)" -ForegroundColor DarkGray
        Write-Host ""

        $userInput = Read-Host "Nhập Steam Link / SteamID / Nickname"
        if ([string]::IsNullOrWhiteSpace($userInput)) {
            continue
        }

        $userInput = $userInput.Trim()

        if ($userInput -eq "exit" -or $userInput -eq "q") {
            Write-Host "Tạm biệt!" -ForegroundColor Green
            break
        }

        if ($userInput -eq "clear" -or $userInput -eq "cls") {
            Write-Header
            continue
        }

        $target = Resolve-SteamInput -inputStr $userInput
        Test-FaceitPlayer -apiKey $apiKey -target $target

        Write-Host ""
        Write-Host "---------------------------------------------------------------" -ForegroundColor DarkGray
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    Start-FaceitTester
}
