# Kurulum paketini ve tasinabilir surumu uretir, sonra Surumler klasorune arsivler.
# Calistirma: powershell -File paket\paketle.ps1
# Surum numarasi tek yerden okunur: src\TurkiyeTakvimi.Arayuz\TurkiyeTakvimi.Arayuz.csproj icindeki <Version>.
$ErrorActionPreference = 'Stop'
$kok = Split-Path $PSScriptRoot -Parent
$yayin = Join-Path $PSScriptRoot 'yayin'
$cikti = Join-Path $PSScriptRoot 'Cikti'
$arsiv = Join-Path $PSScriptRoot 'Surumler'
$iscc = "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe"

# Surum: csproj'daki 2026.10.0 -> gorunen "2026.10" (ucuncu bolum 0 degilse "2026.10.1"), dosya surumu "2026.10.0.0"
$csproj = Join-Path $kok 'src\TurkiyeTakvimi.Arayuz\TurkiyeTakvimi.Arayuz.csproj'
$v = [version](([xml](Get-Content $csproj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
$surum = if ($v.Build -gt 0) { "$($v.Major).$($v.Minor).$($v.Build)" } else { "$($v.Major).$($v.Minor)" }
$dosyaSurumu = "$($v.Major).$($v.Minor).$([math]::Max($v.Build,0)).0"
Write-Host "Surum: $surum (dosya surumu $dosyaSurumu)"

Set-Location $kok
Write-Host '1/5 Testler'
dotnet test --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Testler basarisiz.' }

Write-Host '2/5 Yayimlama (kendi icinde .NET, win-x64)'
if (Test-Path $yayin) { Remove-Item $yayin -Recurse -Force }
dotnet publish src\TurkiyeTakvimi.Arayuz -c Release -r win-x64 --self-contained true `
    -p:DebugType=none -p:DebugSymbols=false -p:SatelliteResourceLanguages=tr -o $yayin --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Yayimlama basarisiz.' }
Copy-Item (Join-Path $kok 'LICENSE') (Join-Path $yayin 'LICENSE.txt') -Force

Write-Host '3/5 Kurulum paketi'
if (Test-Path $cikti) { Remove-Item $cikti -Recurse -Force }
New-Item -ItemType Directory $cikti | Out-Null
& $iscc "/DSurum=$surum" "/DDosyaSurumu=$dosyaSurumu" (Join-Path $PSScriptRoot 'TurkiyeTakvimi.iss')
if ($LASTEXITCODE -ne 0) { throw 'Kurulum paketi derlenemedi.' }

Write-Host '4/5 Tasinabilir surum'
$tasi = Join-Path $PSScriptRoot "TurkiyeTakvimi-$surum-Tasinabilir"
if (Test-Path $tasi) { Remove-Item $tasi -Recurse -Force }
Copy-Item $yayin $tasi -Recurse
Copy-Item (Join-Path $PSScriptRoot 'tasinabilir\tasinabilir.txt') $tasi
Copy-Item (Join-Path $PSScriptRoot 'tasinabilir\Beni-Oku.txt') $tasi
$zip = Join-Path $cikti "TurkiyeTakvimi-$surum-Tasinabilir.zip"
Push-Location $PSScriptRoot
tar -a -c -f $zip "TurkiyeTakvimi-$surum-Tasinabilir"
Pop-Location
Remove-Item $tasi -Recurse -Force

Write-Host '5/5 Arsivleme'
# Ozet dosyasi (SHA-256): indirenin dosyanin bozulmadigini dogrulayabilmesi icin.
$ozet = Join-Path $cikti "TurkiyeTakvimi-$surum-Ozet.txt"
Get-ChildItem $cikti -File | Where-Object { $_.Name -notlike '*-Ozet.txt' } | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash, $_.Name
} | Set-Content $ozet -Encoding ascii
# Arsiv klasorunde ayni surum varsa ESKISI SILINMEZ, tarih eklenerek yeniden adlandirilir.
New-Item -ItemType Directory -Force $arsiv | Out-Null
$hedef = Join-Path $arsiv $surum
if (Test-Path $hedef) {
    $yeniAd = '{0}-eski-{1:yyyyMMdd-HHmmss}' -f $surum, (Get-Date)
    Rename-Item $hedef $yeniAd
    Write-Host "Ayni surumun eski arsivi korundu: Surumler\$yeniAd"
}
Copy-Item $cikti $hedef -Recurse

Get-ChildItem $hedef | ForEach-Object { '{0}  {1:N1} MB' -f $_.Name, ($_.Length / 1MB) }
Write-Host "Arsiv: $hedef"
