; Türkiye Takvimi kurulum betiği (Inno Setup 6).
; Derleme: paket\paketle.ps1 betiği önce uygulamayı yayımlar, sonra bu betiği derler.

#define Ad "Türkiye Takvimi"
#ifndef Surum
  #define Surum "2026.10.1"
#endif
#ifndef DosyaSurumu
  #define DosyaSurumu "2026.10.1.0"
#endif
#define Yayinci "Salih Eroğul"
#define Exe "TurkiyeTakvimi.exe"

[Setup]
; Sabit kimlik: aynı uygulamanın yeni sürümü eskisinin üstüne kurulur, Program Ekle/Kaldır'da tek satır görünür.
AppId={{6B0E8B7A-3C5D-4F0B-9E2A-7D1C54A8F326}
AppName={#Ad}
AppVersion={#Surum}
AppVerName={#Ad} {#Surum}
AppPublisher={#Yayinci}
AppCopyright=© 2026 {#Yayinci}
VersionInfoVersion={#DosyaSurumu}
VersionInfoProductName={#Ad}
VersionInfoDescription={#Ad} kurulumu
; Kurulum kipi (tüm kullanıcılar / yalnızca bu kullanıcı) Inno'nun hazır düğmeli kutusuyla değil, aşağıda [Code] içinde yazılan
; seçim düğmeli (radyo) sayfayla sorulur. Kurulum yönetici izni olmadan başlar; "tüm kullanıcılar" seçilirse kurulum yönetici izniyle yeniden başlatılır.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
DefaultDirName={autopf}\{#Ad}
DisableProgramGroupPage=yes
DisableDirPage=no
DisableReadyPage=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=Cikti
OutputBaseFilename=TurkiyeTakvimi-{#Surum}-Kurulum
SetupIconFile=..\src\TurkiyeTakvimi.Arayuz\Varliklar\uygulama.ico
UninstallDisplayIcon={app}\{#Exe}
UninstallDisplayName={#Ad}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Çalışan uygulama varsa kurulum/kaldırma onu kapatmayı önerir.
AppMutex=Local\TurkiyeTakvimi-tek-kopya
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=no
LanguageDetectionMethod=none

[Languages]
Name: "turkce"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "baslatmenusu"; Description: "Başlat menüsüne kısayol ekle"; GroupDescription: "Kısayollar:"
Name: "masaustu"; Description: "Masaüstüne kısayol ekle"; GroupDescription: "Kısayollar:"; Flags: unchecked

[Files]
Source: "yayin\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#Ad}"; Filename: "{app}\{#Exe}"; Tasks: baslatmenusu
Name: "{autodesktop}\{#Ad}"; Filename: "{app}\{#Exe}"; Tasks: masaustu

[Run]
Filename: "{app}\{#Exe}"; Description: "{#Ad} uygulamasını şimdi çalıştır"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Kaldırınca bilgisayarda iz kalmasın: ayarlar, önbellek, hata kaydı ve paylaşım için üretilen geçici resimler silinir.
Type: filesandordirs; Name: "{localappdata}\TurkiyeTakvimi"
Type: files; Name: "{%TEMP}\Aylik-vakitler-*.png"
Type: files; Name: "{%TEMP}\Imsakiye-*.png"
Type: filesandordirs; Name: "{app}"

[Code]
const
  KurulumKaydi = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{6B0E8B7A-3C5D-4F0B-9E2A-7D1C54A8F326}_is1';

var
  KipSayfasi: TWizardPage;
  TumKullanicilarDugmesi, BuKullaniciDugmesi: TNewRadioButton;
  YenidenBaslatiliyor: Boolean;

function KipKomutSatirindaVerilmis(): Boolean;
var
  Satir: String;
begin
  Satir := Uppercase(GetCmdTail);
  Result := (Pos('/ALLUSERS', Satir) > 0) or (Pos('/CURRENTUSER', Satir) > 0);
end;

// Kurulumu yönetici haklarıyla yeniden başlatır. Hata olursa Hata'ya Windows hata kodu yazılır (1223 = izin penceresi reddedildi).
// Çalışan kurulum programı kendi dosyasını doğrudan yükseltilmiş olarak başlatamaz (Windows "Erişim engellendi (5)" der),
// bu yüzden istek Windows PowerShell üzerinden yapılır; o da olmazsa doğrudan yükseltme denenir.
function YoneticiOlarakYenidenBaslat(var Hata: Integer): Boolean;
var
  Kurulum, Komut: String;
  Kod: Integer;
begin
  Hata := 0;
  Result := False;
  Kurulum := ExpandConstant('{srcexe}');

  Komut := Kurulum;
  StringChangeEx(Komut, '''', '''''', True);
  Komut := '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command "try { Start-Process -FilePath ''' + Komut +
    ''' -ArgumentList ''/ALLUSERS'' -Verb RunAs -ErrorAction Stop } catch { $c = $_.Exception.NativeErrorCode; if ($c -eq 0) { $c = 1 }; exit $c }"';
  if Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Komut, '', SW_HIDE, ewWaitUntilTerminated, Kod) then
  begin
    if Kod = 0 then
    begin
      Result := True;
      Exit;
    end;
    Hata := Kod;
    if Kod = 1223 then Exit; // kullanıcı Windows izin penceresini reddetti; başka yol denenmez
  end
  else
    Hata := Kod;

  Result := ShellExec('runas', Kurulum, '/ALLUSERS', '', SW_SHOWNORMAL, ewNoWait, Kod);
  if Result then
    Hata := 0
  else
    Hata := Kod;
end;
function InitializeSetup(): Boolean;
var
  Hata: Integer;
begin
  Result := True;
  // Daha önce "tüm kullanıcılar için" kurulmuşsa güncelleme de yönetici haklarıyla yapılır; seçim sorulmaz.
  if (not IsAdminInstallMode) and (not KipKomutSatirindaVerilmis()) and RegKeyExists(HKLM64, KurulumKaydi) then
    if YoneticiOlarakYenidenBaslat(Hata) then
      Result := False;
end;

procedure InitializeWizard();
begin
  KipSayfasi := CreateCustomPage(wpWelcome, 'Kurulum kipini seçin', 'Türkiye Takvimi kimler için kurulsun?');

  TumKullanicilarDugmesi := TNewRadioButton.Create(KipSayfasi);
  TumKullanicilarDugmesi.Parent := KipSayfasi.Surface;
  TumKullanicilarDugmesi.Caption := 'Tüm kullanıcılar için kur (önerilir; yönetici izni gerekir)';
  TumKullanicilarDugmesi.Left := 0;
  TumKullanicilarDugmesi.Top := ScaleY(8);
  TumKullanicilarDugmesi.Width := KipSayfasi.SurfaceWidth;
  TumKullanicilarDugmesi.Height := ScaleY(20);
  TumKullanicilarDugmesi.Checked := True;
  TumKullanicilarDugmesi.TabStop := True;

  BuKullaniciDugmesi := TNewRadioButton.Create(KipSayfasi);
  BuKullaniciDugmesi.Parent := KipSayfasi.Surface;
  BuKullaniciDugmesi.Caption := 'Yalnızca geçerli kullanıcı için kur';
  BuKullaniciDugmesi.Left := 0;
  BuKullaniciDugmesi.Top := ScaleY(36);
  BuKullaniciDugmesi.Width := KipSayfasi.SurfaceWidth;
  BuKullaniciDugmesi.Height := ScaleY(20);
end;

// Sayfa açılınca odak seçim düğmelerine gider: aşağı/yukarı ok seçeneği değiştirir; Tab sırasıyla Sonraki ve İptal'e geçer.
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = KipSayfasi.ID then
    WizardForm.ActiveControl := TumKullanicilarDugmesi;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = KipSayfasi.ID then
    Result := IsAdminInstallMode or KipKomutSatirindaVerilmis() or RegKeyExists(HKCU, KurulumKaydi) or RegKeyExists(HKLM64, KurulumKaydi);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Hata: Integer;
begin
  Result := True;
  if (CurPageID = KipSayfasi.ID) and TumKullanicilarDugmesi.Checked and (not IsAdminInstallMode) then
  begin
    Result := False;
    if YoneticiOlarakYenidenBaslat(Hata) then
    begin
      // Yönetici haklarıyla açılan yeni kurulum devralır, bu pencere kapanır.
      YenidenBaslatiliyor := True;
      WizardForm.Close;
    end
    else if Hata = 1223 then
      MsgBox('Yönetici izni verilmedi. Tüm kullanıcılar için kurmak yönetici izni gerektirir. İzin vermek istemiyorsanız "Yalnızca geçerli kullanıcı için kur" seçeneğini seçin.', mbInformation, MB_OK)
    else
      MsgBox('Kurulum yönetici haklarıyla yeniden başlatılamadı. Hata kodu: ' + IntToStr(Hata) + ' (' + SysErrorMessage(Hata) + '). "Yalnızca geçerli kullanıcı için kur" seçeneğiyle kuruluma devam edebilirsiniz.', mbError, MB_OK);
  end;
end;

procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if YenidenBaslatiliyor then Confirm := False;
end;
function InitializeUninstall(): Boolean;
var
  SonucKodu: Integer;
begin
  // Bildirim alanında çalışıyor olabilir; kaldırma kendiliğinden kapatır (kullanıcı kaldırmayı kendisi başlattı).
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#Exe}', '', SW_HIDE, ewWaitUntilTerminated, SonucKodu);
  Sleep(1500);
  Result := True;
end;

// Tüm kullanıcılar için kurulduysa, bu bilgisayardaki her kullanıcının verisi ve "Windows ile başlat" kaydı da silinir.
procedure TumKullanicilarinIzleriniSil();
var
  Bul: TFindRec;
  Ad: String;
  Anahtarlar: TArrayOfString;
  I: Integer;
begin
  if FindFirst(ExpandConstant('{sd}\Users\*'), Bul) then
  begin
    try
      repeat
        Ad := Bul.Name;
        if (Bul.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) and (Ad <> '.') and (Ad <> '..') then
          DelTree(ExpandConstant('{sd}\Users\') + Ad + '\AppData\Local\TurkiyeTakvimi', True, True, True);
      until not FindNext(Bul);
    finally
      FindClose(Bul);
    end;
  end;
  if RegGetSubkeyNames(HKEY_USERS, '', Anahtarlar) then
    for I := 0 to GetArrayLength(Anahtarlar) - 1 do
      RegDeleteValue(HKEY_USERS, Anahtarlar[I] + '\Software\Microsoft\Windows\CurrentVersion\Run', 'TurkiyeTakvimi');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // "Windows ile birlikte başlat" ayarının kayıt satırı.
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'TurkiyeTakvimi');
    if IsAdminInstallMode then TumKullanicilarinIzleriniSil();
  end;
end;
