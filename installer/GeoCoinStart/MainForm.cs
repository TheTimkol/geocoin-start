using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Drawing.Imaging;
using QRCoder;
using ZXing.Common;
using ZXing.Windows.Compatibility;

namespace GeoCoinStart;

internal sealed class MainForm : Form
{
    private static class NativeTheme
    {
        [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public static extern int SetWindowTheme(IntPtr hwnd, string applicationName, string subIdList);
    }

    private class ThemeAwarePanel : Panel
    {
        public void RecreateThemeHandle()
        {
            if (IsHandleCreated) RecreateHandle();
        }
    }

    private sealed class ThemeAwareFlowLayoutPanel : FlowLayoutPanel
    {
        public void RecreateThemeHandle()
        {
            if (IsHandleCreated) RecreateHandle();
        }
    }

    private class ThemeAwareComboBox : ComboBox
    {
        public void RecreateThemeHandle()
        {
            if (IsHandleCreated) RecreateHandle();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _ = NativeTheme.SetWindowTheme(Handle, string.Empty, string.Empty);
        }
    }

    private sealed class PageWheelComboBox : ThemeAwareComboBox
    {
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Action<int>? ForwardWheelToPage { get; set; }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (DroppedDown)
            {
                base.OnMouseWheel(e);
                return;
            }

            ForwardWheelToPage?.Invoke(e.Delta);
        }
    }

    private class ThemeAwareCheckedListBox : CheckedListBox
    {
        public void RefreshThemeDrawing()
        {
            if (IsHandleCreated) Invalidate(true);
        }
    }

    private sealed class ThemeAwareNumericUpDown : NumericUpDown
    {
        public void RefreshThemeDrawing()
        {
            if (IsHandleCreated) Invalidate(true);
        }
    }

    private sealed class PageWheelCheckedListBox : ThemeAwareCheckedListBox
    {
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Action<int>? ForwardWheelToPage { get; set; }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int visibleRows = Math.Max(1, ClientSize.Height / Math.Max(1, ItemHeight));
            bool canScrollList = Items.Count > visibleRows &&
                (e.Delta > 0 ? TopIndex > 0 : TopIndex + visibleRows < Items.Count);
            if (canScrollList)
            {
                base.OnMouseWheel(e);
                return;
            }

            ForwardWheelToPage?.Invoke(e.Delta);
        }
    }

    private sealed class GuideProgressTile : Panel
    {
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int StepNumber { get; init; }
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int ProgressPercent { get; init; }
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsCurrent { get; init; }
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; init; }
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color TrackColor { get; init; }
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color ProgressColor { get; init; }

        public GuideProgressTile()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float scale = Math.Max(1f, DeviceDpi / 96f);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            using (var border = new Pen(BorderColor, Math.Max(1f, scale)))
                e.Graphics.DrawRectangle(border, bounds);

            int iconSize = Math.Max(16, (int)Math.Round(20 * scale));
            int iconX = (int)Math.Round(10 * scale);
            int iconY = (int)Math.Round(10 * scale);
            var icon = new Rectangle(iconX, iconY, iconSize, iconSize);
            if (ProgressPercent >= 100)
            {
                using var fill = new SolidBrush(ProgressColor);
                e.Graphics.FillEllipse(fill, icon);
                using var check = new Pen(Color.White, Math.Max(1.5f, 2f * scale)) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
                e.Graphics.DrawLines(check, new[]
                {
                    new PointF(icon.Left + iconSize * 0.24f, icon.Top + iconSize * 0.53f),
                    new PointF(icon.Left + iconSize * 0.43f, icon.Top + iconSize * 0.70f),
                    new PointF(icon.Left + iconSize * 0.77f, icon.Top + iconSize * 0.32f)
                });
            }
            else
            {
                using var outline = new Pen(IsCurrent ? ProgressColor : BorderColor, Math.Max(1.3f, 1.7f * scale));
                e.Graphics.DrawEllipse(outline, icon);
                TextRenderer.DrawText(e.Graphics, StepNumber.ToString(CultureInfo.InvariantCulture), Font, icon, ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }

            int titleX = icon.Right + (int)Math.Round(8 * scale);
            int titleY = (int)Math.Round(7 * scale);
            var titleBounds = new Rectangle(titleX, titleY, Math.Max(12, Width - titleX - (int)Math.Round(8 * scale)),
                Math.Max(iconSize, (int)Math.Round(26 * scale)));
            TextRenderer.DrawText(e.Graphics, Text, Font, titleBounds, ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            int barY = Height - (int)Math.Round(14 * scale);
            int percentWidth = (int)Math.Round(38 * scale);
            var track = new Rectangle(iconX, barY, Math.Max(8, Width - iconX - percentWidth - (int)Math.Round(14 * scale)),
                Math.Max(3, (int)Math.Round(4 * scale)));
            using (var trackBrush = new SolidBrush(TrackColor)) e.Graphics.FillRectangle(trackBrush, track);
            int progressWidth = (int)Math.Round(track.Width * Math.Clamp(ProgressPercent, 0, 100) / 100d);
            if (progressWidth > 0)
            {
                using var progressBrush = new SolidBrush(ProgressColor);
                e.Graphics.FillRectangle(progressBrush, new Rectangle(track.Location, new Size(progressWidth, track.Height)));
            }
            var percentBounds = new Rectangle(track.Right + (int)Math.Round(5 * scale), barY - (int)Math.Round(7 * scale),
                percentWidth, (int)Math.Round(18 * scale));
            TextRenderer.DrawText(e.Graphics, $"{Math.Clamp(ProgressPercent, 0, 100)}%", Font, percentBounds, ForeColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    private sealed class UserActivityMessageFilter(Action onActivity) : IMessageFilter
    {
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg is 0x0100 or 0x0104 or 0x0200 or 0x0201 or 0x0204 or 0x0207 or 0x020A)
                onActivity();
            return false;
        }
    }

    private sealed record GpuDevice(string Backend, string Id, string Name)
    {
        public override string ToString() => $"{Name} — {Backend.ToUpperInvariant()} · {Id}";
    }

    private sealed record FeeEstimate(decimal ApproximateGc, decimal MinimumGc, decimal RateGcPerKb, int AssumedBytes);

    private sealed record PeerDiagnostic(
        string Address,
        bool Connected,
        bool PortRefused,
        bool OtherFailure,
        bool InProgress,
        bool Inbound,
        bool Encrypted,
        string State,
        int PingMilliseconds,
        int FailureCount,
        string Error);

    private sealed record MinedBlockRecord(string BlockId, string TransactionId, long Timestamp, string Source);
    private sealed class MinedBlockLedger
    {
        public List<MinedBlockRecord> Blocks { get; set; } = new();
        public long? SessionStartedAtUnix { get; set; }
        public long? SessionEndedAtUnix { get; set; }
        public List<string> SessionBlockIds { get; set; } = new();
        public List<string> LifetimeBlockIds { get; set; } = new();
        public Dictionary<string, string> TransactionToBlockIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool WalletHistoryBackfilled { get; set; }
    }

    private static readonly Color LightCanvas = Color.FromArgb(241, 241, 235);
    private static readonly Color LightInk = Color.FromArgb(31, 39, 34);
    private static readonly Color LightMuted = Color.FromArgb(104, 115, 105);
    private static readonly Color LightRail = Color.FromArgb(22, 31, 26);
    private static readonly Color DarkCanvas = Color.FromArgb(30, 35, 32);
    private static readonly Color DarkSurface = Color.FromArgb(39, 46, 41);
    private static readonly Color DarkInput = Color.FromArgb(47, 55, 49);
    private static readonly Color DarkInk = Color.FromArgb(235, 238, 231);
    private static readonly Color DarkMuted = Color.FromArgb(174, 185, 173);
    private static readonly Color DarkRail = Color.FromArgb(17, 23, 20);
    private static readonly Color DarkAccent = Color.FromArgb(184, 145, 76);
    private static readonly Color ModernLightCanvas = Color.FromArgb(239, 245, 242);
    private static readonly Color ModernLightRail = Color.FromArgb(224, 235, 230);
    private static readonly Color ModernLightInk = Color.FromArgb(27, 43, 39);
    private static readonly Color ModernLightMuted = Color.FromArgb(82, 105, 97);
    private static readonly Color ModernLightAccent = Color.FromArgb(20, 126, 108);
    private static readonly Color ModernDarkCanvas = Color.FromArgb(21, 29, 31);
    private static readonly Color ModernDarkSurface = Color.FromArgb(32, 43, 45);
    private static readonly Color ModernDarkInput = Color.FromArgb(41, 55, 57);
    private static readonly Color ModernDarkInk = Color.FromArgb(234, 243, 240);
    private static readonly Color ModernDarkMuted = Color.FromArgb(165, 188, 180);
    private static readonly Color ModernDarkRail = Color.FromArgb(15, 23, 25);
    private static readonly Color ModernDarkAccent = Color.FromArgb(69, 194, 173);
    private Color Canvas => _modernDesign ? (_darkTheme ? ModernDarkCanvas : ModernLightCanvas) : (_darkTheme ? DarkCanvas : LightCanvas);
    private Color Surface => _modernDesign ? (_darkTheme ? ModernDarkSurface : Color.White) : (_darkTheme ? DarkSurface : Color.White);
    private Color InputSurface => _modernDesign ? (_darkTheme ? ModernDarkInput : Color.FromArgb(248, 251, 249)) : (_darkTheme ? DarkInput : Color.FromArgb(250, 250, 247));
    private Color Ink => _modernDesign ? (_darkTheme ? ModernDarkInk : ModernLightInk) : (_darkTheme ? DarkInk : LightInk);
    private Color Muted => _modernDesign ? (_darkTheme ? ModernDarkMuted : ModernLightMuted) : (_darkTheme ? DarkMuted : LightMuted);
    private Color Accent => _modernDesign ? (_darkTheme ? ModernDarkAccent : ModernLightAccent) : (_darkTheme ? DarkAccent : Color.FromArgb(166, 129, 63));
    private Color Rail => _modernDesign ? (_darkTheme ? ModernDarkRail : ModernLightRail) : (_darkTheme ? DarkRail : LightRail);
    private Color Divider => _modernDesign
        ? (_darkTheme ? Color.FromArgb(53, 69, 68) : Color.FromArgb(207, 222, 216))
        : (_darkTheme ? Color.FromArgb(61, 68, 62) : Color.FromArgb(220, 220, 214));
    private Color Success => _modernDesign
        ? (_darkTheme ? Color.FromArgb(119, 211, 164) : Color.FromArgb(35, 119, 79))
        : (_darkTheme ? Color.FromArgb(118, 194, 133) : Color.FromArgb(53, 119, 74));
    private Color Error => _modernDesign
        ? (_darkTheme ? Color.FromArgb(240, 137, 126) : Color.FromArgb(164, 62, 53))
        : (_darkTheme ? Color.FromArgb(240, 137, 116) : Color.FromArgb(161, 70, 53));

    private static readonly Dictionary<string, string> RussianToEnglish = new(StringComparer.Ordinal)
    {
        ["GeoCoin-Start — простой кошелёк"] = "GeoCoin-Start — simple wallet",
        ["ПРОСТОЙ КОШЕЛЁК"] = "SIMPLE WALLET",
        ["Обзор"] = "Overview", ["Майнинг"] = "Mining", ["Подмайнер"] = "Subminer", ["Нода"] = "Node",
        ["Отправить"] = "Send", ["Получить"] = "Receive", ["История"] = "History", ["Кошелёк"] = "Wallet",
        ["Изменить пароль кошелька"] = "Change wallet password", ["Защита паролем кошелька"] = "Wallet password protection",
        ["Задать пароль кошелька"] = "Set wallet password",
        ["Введите текущий пароль кошелька и задайте новый."] = "Enter the current wallet password, then choose a new password.",
        ["Задайте пароль, чтобы зашифровать этот кошелёк. После шифрования нода перезапустится."] = "Choose a password to encrypt this wallet. The node will restart after encryption.",
        ["Текущий пароль кошелька"] = "Current wallet password", ["Новый пароль кошелька"] = "New wallet password",
        ["Повторите новый пароль кошелька"] = "Confirm new wallet password",
        ["Изменить пароль"] = "Change password", ["Задать пароль"] = "Set password",
        ["Введите текущий пароль кошелька."] = "Enter the current wallet password.",
        ["Пароль кошелька не может быть пустым."] = "The wallet password cannot be empty.",
        ["Выберите пароль, отличный от текущего."] = "Choose a password different from the current one.",
        ["Задайте пароль для незашифрованного кошелька или замените текущий пароль. После изменения сохраните новую резервную копию."] = "Set a password on an unencrypted wallet or replace its current password. Save a new backup after changing it.",
        ["Пароль задан, кошелёк зашифрован, но нода ещё работает. Остановите её и запустите снова, введя новый пароль."] = "The password was set and the wallet encrypted, but the node is still running. Stop it and start it again with the new password.",
        ["Новый пароль"] = "New password", ["Неверный пароль"] = "Incorrect password",
        ["Пароль изменён"] = "Password changed", ["Кошелёк защищён"] = "Wallet protected", ["Пароль задан"] = "Password set",
        ["Не удалось изменить пароль кошелька"] = "Could not change wallet password",
        ["Пароль кошелька изменён. Новые резервные копии будут открываться новым паролем; старые копии сохраняют прежний пароль."] = "Wallet password changed. New backups will use the new password; existing backups keep their previous password.",
        ["Пароль задан, кошелёк зашифрован и нода перезапущена. Создайте новую резервную копию и храните пароль отдельно. Старые копии не изменены."] = "Password set, wallet encrypted, and node restarted. Create a new backup and store the password separately. Existing backups are unchanged.",
        ["Не удалось открыть кошелёк. Возможная причина — неверный пароль. Проверьте пароль и раскладку клавиатуры. Если пароль верен, возможно, файл кошелька повреждён. Исходный файл оставлен без изменений."] = "GeoCoin could not open the wallet. The password may be incorrect. Check the password and keyboard layout. If it is correct, the wallet file may be damaged. The original file was left unchanged.",
        ["Неверный пароль кошелька. Проверьте его и попробуйте снова."] = "Incorrect wallet password. Check it and try again.",
        ["Этот кошелёк пока не защищён паролем. Установка пароля зашифрует его и перезапустит ноду; работающий майнинг остановится. Перед продолжением создайте резервную копию. Продолжить?"] = "This wallet is not password-protected yet. Setting a password encrypts it and restarts the node; active mining will stop. Create a backup before continuing. Continue?",
        ["Пароль кошелька задан, но нода не смогла запуститься автоматически. Запустите её со страницы «Обзор» и введите новый пароль."] = "The wallet password was set, but the node could not restart automatically. Start it on the Overview page and enter the new password.",
        ["Не удалось определить, защищён ли кошелёк паролем. Обновите состояние ноды и попробуйте снова."] = "Could not determine whether the wallet is password-protected. Refresh the node status and try again.",
        ["Смена пароля требует запущенной локальной ноды и открытого кошелька."] = "Changing the password requires a running local node and an open wallet.",
        ["Кошелёк зашифрован"] = "Wallet encrypted",
        ["Не удалось открыть кошелёк"] = "Could not open wallet",
        ["Попробовать ещё раз"] = "Retry",
        ["Повторить"] = "Retry",
        ["Помощник"] = "Guide", ["Настройки"] = "Settings", ["Дизайн"] = "Design", ["Помощник первого запуска"] = "First-run guide",
        ["Переключить на современный стиль"] = "Switch to modern design",
        ["Переключить на классический стиль"] = "Switch to classic design",
        ["Шаг 1 из 4 · Подключите ноду"] = "Step 1 of 4 · Connect the node",
        ["Шаг 2 из 4 · Создайте или добавьте кошелёк"] = "Step 2 of 4 · Create or add a wallet",
        ["Шаг 3 из 4 · Сохраните резервную копию"] = "Step 3 of 4 · Save a backup",
        ["Шаг 4 из 4 · Завершите обучение"] = "Step 4 of 4 · Finish the guide",
        ["Обучение пройдено"] = "Training completed",
        ["Обучение пройдено. Ваш прогресс сохранён, пользоваться приложением можно через меню."] = "Training completed. Your progress is saved. Use the app menu to continue.",
        ["Обучение завершено. Статус сохранён на этом компьютере; при следующем открытии помощника вы увидите этот результат."] = "Training completed. Your status is saved on this computer and will appear when you reopen the guide.",
        ["Основные шаги выполнены. Майнинг и создание адреса остаются на ваше усмотрение. Нажмите кнопку, чтобы сохранить прохождение помощника."] = "The required steps are complete. Mining and creating a receiving address are optional. Finish the guide to save your progress.",
        ["БЕЗОПАСНОСТЬ"] = "SECURITY", ["Длина PIN"] = "PIN length", ["Текущий PIN"] = "Current PIN",
        ["Новый PIN"] = "New PIN", ["Повторите новый PIN"] = "Confirm new PIN",
        ["СИСТЕМНЫЕ УВЕДОМЛЕНИЯ"] = "SYSTEM NOTIFICATIONS", ["Показывать уведомления Windows"] = "Show Windows notifications",
        ["Сохранить уведомления"] = "Save notification settings",
        ["Входящий перевод"] = "Incoming transfer",
        ["Отправленный перевод принят нодой"] = "Outgoing transfer accepted by node",
        ["АВТОЗАПУСК"] = "STARTUP",
        ["Запускать GeoCoin-Start при входе в Windows"] = "Start GeoCoin-Start when I sign in to Windows",
        ["Автозапуск открывает только приложение; запуск ноды и майнинга остаётся под вашим контролем."] = "Autostart only opens the app; the node and mining remain under your control.",
        ["Ошибка автозапуска"] = "Startup setting error",
        ["Отправить тестовое уведомление"] = "Send a test notification",
        ["Включите и сохраните системные уведомления, чтобы отправить тест."] = "Enable and save system notifications to send a test.",
        ["Уведомления выключены"] = "Notifications are off",
        ["Тестовое уведомление GeoCoin-Start"] = "GeoCoin-Start test notification",
        ["Системные уведомления приложения включены."] = "System notifications are enabled.",
        ["Нода остановилась или потеряла соединение"] = "Node stopped or lost connection",
        ["Нода завершила синхронизацию"] = "Node finished syncing", ["Обнаружена новая награда за майнинг"] = "A new mining reward was detected",
        ["Новый входящий перевод GeoCoin"] = "New incoming GeoCoin transfer",
        ["Входящий перевод появился в истории кошелька. Откройте историю для подробностей."] = "A new incoming transfer appeared in wallet history. Open History for details.",
        ["Перевод отправлен"] = "Transfer sent",
        ["Нода приняла перевод к отправке. Он ещё не подтверждён в блоке."] = "The node accepted the transfer for broadcast. It is not confirmed in a block yet.",
        ["Автоматически блокировать после 10 минут бездействия"] = "Lock automatically after 10 minutes of inactivity",
        ["Включить PIN и сохранить"] = "Enable PIN and save", ["Сохранить PIN и настройки"] = "Save PIN and settings",
        ["Настроить PIN-защиту"] = "Set up PIN protection",
        ["Отключить PIN"] = "Disable PIN", ["Заблокировать"] = "Lock now", ["Разблокировать"] = "Unlock",
        ["Windows Hello"] = "Windows Hello", ["Разрешить разблокировку с помощью Windows Hello"] = "Allow unlocking with Windows Hello",
        ["GeoCoin-Start заблокирован"] = "GeoCoin-Start is locked",
        ["Введите текущий PIN, чтобы изменить способ разблокировки."] = "Enter the current PIN to change the unlock method.",
        ["Windows Hello доступен начиная с Windows 11. Используйте PIN-код приложения."] = "Windows Hello is available on Windows 11 or later. Use the app PIN.",
        ["Подтвердите личность, чтобы разблокировать GeoCoin-Start."] = "Verify your identity to unlock GeoCoin-Start.",
        ["Windows Hello не подтвердил вход. Можно попробовать ещё раз или ввести PIN."] = "Windows Hello did not verify you. Try again or enter your PIN.",
        ["Windows Hello недоступен. Используйте PIN-код приложения."] = "Windows Hello is unavailable. Use the app PIN.",
        ["Открытие кошелька"] = "Open wallet", ["Создание кошелька"] = "Create wallet",
        ["Подтвердите действие"] = "Confirm action", ["Пароль кошелька"] = "Wallet password",
        ["Повторите пароль"] = "Confirm password", ["Отмена"] = "Cancel", ["Продолжить"] = "Continue",
        ["Да"] = "Yes", ["Нет"] = "No", ["ОК"] = "OK",
        ["Создать"] = "Create", ["Защита кошелька"] = "Wallet security", ["Пароли не совпадают."] = "The passwords do not match.",
        ["Введите пароль этого файла кошелька. Если файл не защищён паролем, оставьте поле пустым."] = "Enter this wallet file's password. If the file is not password-protected, leave the field empty.",
        ["Придумайте пароль и сохраните его отдельно. Без него открыть кошелёк и отправить монеты не получится."] = "Choose a password and store it separately. You will need it to open the wallet and send coins.",
        ["Введите пароль кошелька, чтобы создать адрес для получения."] = "Enter the wallet password to create a receiving address.",
        ["Введите пароль кошелька, чтобы импортировать приватный ключ."] = "Enter the wallet password to import a private key.",
        ["Введите пароль кошелька, чтобы экспортировать приватный ключ выбранного адреса."] = "Enter the wallet password to export the selected address's private key.",
        ["Я сохранил резервную копию"] = "I saved a backup", ["Завершить помощник"] = "Finish guide",
        ["Резервная копия"] = "Backup", ["Первый перевод или майнинг"] = "First transfer or mining",
        ["Перевод или майнинг"] = "Transfer or mining",
        ["Пока пропустить помощник"] = "Skip the guide for now", ["Открыть помощник первого запуска"] = "Open the first-run guide",
        ["Открыть резервные копии"] = "Open wallet backup", ["Экспортировать копию сейчас"] = "Export a backup now",
        ["Этот формат кошелька нужно копировать при остановленной ноде. Сначала остановите майнинг, затем повторите резервное копирование."] = "This wallet format must be backed up while the node is stopped. Stop mining first, then try again.",
        ["Остановите майнинг"] = "Stop mining",
        ["Этот файл кошелька нельзя скопировать через RPC. GeoCoin-Start может безопасно остановить ноду, скопировать файл и затем запустить ноду снова. При запуске может потребоваться пароль кошелька. Продолжить?"] = "This wallet file cannot be backed up through RPC. GeoCoin-Start can safely stop the node, copy the file, and restart the node. You may need the wallet password when it starts. Continue?",
        ["Создать резервную копию"] = "Create a wallet backup",
        ["Копия кошелька сохранена. Храните её отдельно от компьютера; для защищённого файла также сохраните пароль."] = "The wallet backup was saved. Store it separately from this computer, and keep the password for an encrypted wallet file.",
        ["Копия готова"] = "Backup complete",
        ["Копия создана, но нода осталась остановлена. Запустите её снова со страницы «Нода»."] = "The backup was created, but the node is still stopped. Start it again from the Node page.",
        ["Не удалось безопасно создать копию кошелька: "] = "Could not safely create the wallet backup: ",
        ["Не удалось создать копию"] = "Could not create backup",
        ["Записей на странице"] = "Rows per page", ["Показаны записи "] = "Showing entries ",
        ["Записей:"] = "Rows:",
        ["Управляйте защитой приложения, системными уведомлениями, автозапуском и первым запуском."] = "Manage app security, system notifications, startup, and the first-run guide.",
        ["PIN-код ограничивает доступ к окну приложения. Для защиты средств используйте пароль кошелька и храните резервную копию отдельно."] = "The PIN limits access to the app window. Protect your funds with the wallet password and store a backup separately.",
        ["Перейти к майнингу"] = "Go to mining", ["Открыть получение монет"] = "Open Receive",
        ["Запустить ноду"] = "Start node", ["Создать кошелёк"] = "Create wallet",
        ["Импортировать кошелёк"] = "Import wallet", ["Открыть страницу ноды"] = "Open node page",
        ["Текущая информация"] = "Current information", ["Введите PIN-код приложения"] = "Enter your app PIN",
        ["PIN-защита включена для текущей учётной записи Windows."] = "PIN protection is enabled for this Windows account.",
        ["PIN-защита выключена."] = "PIN protection is off.",
        ["PIN-код блокирует доступ к окну GeoCoin-Start, но не заменяет пароль кошелька и не шифрует файл кошелька."] = "The PIN locks the GeoCoin-Start window, but does not replace the wallet password or encrypt the wallet file.",
        ["PIN-код блокирует доступ к окну GeoCoin-Start, но не заменяет пароль кошелька и не шифрует файл кошелька. Настроить PIN и уведомления можно в разделе «Настройки»."] = "The PIN locks the GeoCoin-Start window, but does not replace the wallet password or encrypt the wallet file. Configure the PIN and notifications in Settings.",
        ["Настройки безопасности сохранены. PIN блокирует окно GeoCoin-Start и не заменяет пароль кошелька."] = "Security settings saved. The PIN locks the GeoCoin-Start window and does not replace the wallet password.",
        ["Настройки уведомлений сохранены."] = "Notification settings saved.", ["Настройки сохранены"] = "Settings saved",
        ["Автоматически блокировать"] = "Lock automatically", ["минут бездействия"] = "minutes of inactivity",
        ["Windows заблокировала системные уведомления приложения. Проверьте настройки уведомлений Windows."] = "Windows blocked notifications for this app. Check Windows notification settings.",
        ["Проверка уведомлений"] = "Notification check",
        ["Текущий PIN-код указан неверно."] = "The current PIN is incorrect.", ["Проверьте PIN-код"] = "Check the PIN",
        ["Неверная длина PIN"] = "Incorrect PIN length", ["Новый PIN должен содержать ровно "] = "The new PIN must contain exactly ",
        [" цифры."] = " digits.", ["PIN-коды не совпадают."] = "The PIN codes do not match.", ["Проверьте ввод"] = "Check your entry",
        ["Не удалось сохранить PIN-код: "] = "Could not save PIN: ", ["Ошибка защиты"] = "Security error",
        ["Чтобы отключить защиту, введите текущий PIN-код."] = "Enter the current PIN to disable protection.",
        ["Ошибка блокировки"] = "Lock error", ["PIN-код не подходит. Попробуйте ещё раз."] = "That PIN is not correct. Try again.",
        ["Слишком много попыток. Подождите 30 секунд."] = "Too many attempts. Wait 30 seconds.",
        ["сек. перед новой попыткой."] = " seconds before trying again.", ["Слишком много попыток. Подождите "] = "Too many attempts. Wait ",
        ["Открыть GeoCoin-Start"] = "Open GeoCoin-Start", ["Выйти из GeoCoin-Start"] = "Exit GeoCoin-Start",
        ["Новая награда GeoCoin"] = "New GeoCoin reward",
        ["Нода остановлена"] = "Node stopped", ["Потеряно соединение с нодой"] = "Lost connection to the node",
        ["RPC не отвечает. Проверьте состояние ноды и журнал."] = "RPC is not responding. Check node status and the log.",
        ["Процесс GeoCoin завершился. Откройте вкладку «Нода», чтобы посмотреть журнал."] = "The GeoCoin process exited. Open the Node page to view the log.",
        ["История кошелька показывает новый добытый блок."] = "Wallet history shows a newly mined block.",
        ["Нода догнала текущую высоту сети."] = "The node has caught up with the network.",
        ["Нода запущена, RPC не отвечает"] = "Node is running, but RPC is not responding",
        ["Пошаговый помощник проведёт через подключение, кошелёк и первый запуск. Вы можете перейти к любому разделу в меню."] = "This step-by-step guide helps you connect the node, set up a wallet, and get started. You can open any section from the menu.",
        ["Нода подключает GeoCoin-Start к сети и синхронизирует блоки. Нажмите «Запустить ноду» и дождитесь соединения."] = "The node connects GeoCoin-Start to the network and syncs blocks. Select Start node and wait for the connection.",
        ["Создайте новый кошелёк или импортируйте существующий файл .dat. Пароль кошелька потребуется при подключении."] = "Create a wallet or import an existing .dat file. You will need the wallet password when connecting.",
        ["Экспортируйте файл кошелька .dat и храните его отдельно. Резервная копия поможет восстановить доступ при поломке компьютера."] = "Export the wallet .dat file and store it separately. A backup can restore access if your computer fails.",
        ["Теперь можно открыть майнинг, создать адрес для получения или перейти к обзору. Майнинг необязателен."] = "You can now open Mining, create a receive address, or return to Overview. Mining is optional.",
        ["Шаги"] = "Steps", ["Первый перевод или майнинг"] = "First transfer or mining",
        ["Баланс: "] = "Balance: ", ["Добыто блоков: "] = "Blocks mined: ", [" · сегодня: "] = " · today: ",
        [" · сессия: "] = " · session: ", ["Хешрейт: "] = "Hashrate: ", ["CPU: "] = "CPU: ",
        ["GPU: "] = "GPU: ", ["Состояние ноды: "] = "Node status: ", ["Пиров: "] = "Peers: ",
        ["Нода и журнал"] = "Node and log", ["Отправить монеты"] = "Send coins", ["Получить монеты"] = "Receive coins",
        ["История транзакций"] = "Transaction history", ["Управление кошельком"] = "Wallet management",
        ["Управление GeoCoin без лишних настроек"] = "Manage GeoCoin without unnecessary settings",
        ["Оригинальный проект ↗"] = "Original project ↗", ["Редистрибьютор: TheTimkol ↗"] = "Redistributor: TheTimkol ↗",
        ["Тёмная тема"] = "Dark theme", ["Светлая тема"] = "Light theme",
        ["Здесь видно состояние кошелька и синхронизации. Для начала запустите ноду."] = "View your wallet and sync status here. Start the node to begin.",
        ["БАЛАНС КОШЕЛЬКА"] = "WALLET BALANCE", ["СОСТОЯНИЕ СЕТИ"] = "NETWORK STATUS", ["ДОБЫЧА И НАГРАДЫ"] = "MINING AND REWARDS",
        ["Кошелёк ещё не создан"] = "Wallet has not been created",
        ["Запустите ноду, чтобы начать подключение"] = "Start the node to connect",
        ["Добытые блоки за всё время: "] = "Blocks mined all time: ", ["Зрелые монеты: "] = "Mature coins: ", ["Незрелые монеты: "] = "Immature coins: ",
        ["Зрелые монеты: —"] = "Mature coins: —", ["Незрелые монеты: —"] = "Immature coins: —",
        ["Запускаю ноду…"] = "Starting node…", ["Запустить ноду"] = "Start node", ["Остановить ноду"] = "Stop node",
        ["Создать кошелёк"] = "Create wallet", ["Импортировать кошелёк"] = "Import wallet", ["Импортировать другой"] = "Import another wallet",
        ["Экспортировать кошелёк (.dat)"] = "Export wallet (.dat)", ["Экспортировать весь кошелёк (.dat)"] = "Export full wallet (.dat)",
        ["Сделайте резервную копию кошелька и храните её отдельно от компьютера."] = "Back up your wallet and store the copy away from this computer.",
        ["Создайте кошелёк или импортируйте существующий файл GeoCoin. Пароль хранится только у вас."] = "Create a wallet or import an existing GeoCoin file. Only you know its password.",
        ["CPU и GPU настраиваются в одном окне. Их можно запускать по отдельности или одновременно."] = "Set up CPU and GPU mining here. Run either one or both at the same time.",
        ["ДОБЫТЫЕ БЛОКИ"] = "MINED BLOCKS", ["Майнинг на процессоре · CPU"] = "CPU mining", ["Потоки"] = "Threads",
        ["CPU-майнинг включён · потоков: "] = "CPU mining running · threads: ", ["CPU-майнинг остановлен"] = "CPU mining stopped",
        ["Скорость CPU: "] = "CPU hashrate: ", ["Остановить CPU"] = "Stop CPU", ["Запустить CPU"] = "Start CPU",
        ["Майнинг на видеокарте · GPU"] = "GPU mining", ["Выберите видеокарту. Награда поступит на уже существующий адрес этого кошелька."] = "Choose a GPU. Rewards will go to an existing address in this wallet.",
        ["Найти"] = "Find", ["GPU-майнинг остановлен"] = "GPU mining stopped", ["GPU-майнинг запущен. Ожидаю данные о скорости…"] = "GPU mining started. Waiting for hashrate…",
        ["Скорость GPU: —"] = "GPU hashrate: —", ["Скорость GPU: ожидание данных…"] = "GPU hashrate: waiting for data…",
        ["Адрес награды будет взят из текущего кошелька."] = "The reward address will come from the current wallet.", ["Адрес награды: "] = "Reward address: ",
        ["Перейти к подмайнеру"] = "Open subminer", ["Остановить GPU"] = "Stop GPU", ["Запустить GPU"] = "Start GPU",
        ["Сначала запустите ноду на странице «Обзор»."] = "Start the node on the Overview page first.",
        ["Сначала создайте или импортируйте кошелёк на странице «Обзор»."] = "Create or import a wallet on the Overview page first.",
        ["Один процесс GeoCoin выполняет работу ноды. RPC — интерфейс управления этой же нодой; отдельно запускать вторую RPC-ноду не нужно."] = "One GeoCoin process runs the node. RPC controls that same node; a second RPC node is not needed.",
        ["СОСТОЯНИЕ УЗЛА"] = "NODE STATUS", ["ДОСТУП ДЛЯ GPU-ПОДМАЙНЕРА"] = "GPU SUBMINER ACCESS", ["Адрес прослушивания"] = "Listen address",
        ["Пиры сети"] = "Network peers", ["Активные: "] = "Active: ", ["Отказ порта*: "] = "Port refused*: ", ["Другие сбои: "] = "Other errors: ",
        ["Показать список"] = "Show list", ["Скрыть список"] = "Hide list", ["Запустите ноду, чтобы увидеть состояние пиров."] = "Start the node to see peer status.",
        ["Как открыть P2P-порт"] = "How to open the P2P port",
        ["Читаю состояние пиров…"] = "Loading peer status…", ["Отказ соединения часто указывает на закрытый порт или блокировку брандмауэром. Таймаут сам по себе не подтверждает, что порт закрыт."] = "A connection refusal often indicates a closed port or firewall rejection. A timeout alone does not confirm that the port is closed.",
        ["Подробная диагностика недоступна. Показаны только активные подключения."] = "Detailed diagnostics are unavailable. Only active connections are shown.",
        ["Не удалось получить список пиров. Проверьте журнал ноды."] = "Could not retrieve the peer list. Check the node log.",
        ["Нет активных пиров и недавних попыток подключения."] = "No active peers or recent connection attempts.",
        ["Подключено"] = "Connected", ["Входящее"] = "Inbound", ["Исходящее"] = "Outbound", ["Зашифровано"] = "Encrypted", ["Без шифрования"] = "Unencrypted",
        ["Ping: "] = "Ping: ", ["Подключается"] = "Connecting", ["Порт отклоняет соединение"] = "Connection refused", ["Другая ошибка соединения"] = "Other connection error",
        ["попыток: "] = "attempts: ", ["Ошибка: "] = "Error: ", ["Состояние: "] = "State: ",
        ["127.0.0.1 · только этот компьютер"] = "127.0.0.1 · this computer only",
        ["Для доступа с другого компьютера используйте защищённый туннель к порту 3033 на этом компьютере."] = "To connect another computer, use an encrypted tunnel to port 3033 on this computer.",
        ["Имя пользователя майнера"] = "Miner username", ["Пароль майнера"] = "Miner password",
        ["Показать"] = "Show", ["Скрыть"] = "Hide", ["Скопировать данные для подмайнера"] = "Copy subminer connection details",
        ["Для майнинга с другого компьютера подключитесь через защищённый туннель."] = "Connect over an encrypted tunnel to mine from another computer.",
        ["Пароль действует, пока приложение открыто. Если появится запрос брандмауэра, разрешите только частные сети."] = "The password is kept only while the app is open. If Windows Firewall asks, allow private networks only.",
        ["ЖУРНАЛ НОДЫ И МАЙНЕРА"] = "NODE AND MINER LOG", ["Открыть файл журнала"] = "Open log file",
        ["Копировать отчёт"] = "Copy diagnostics", ["Пауза отображения журнала"] = "Pause log display",
        ["Диагностическая сводка скопирована. Она содержит только технический статус, без адресов кошелька и паролей."] = "Diagnostic summary copied. It contains technical status only; wallet addresses and passwords are excluded.",
        ["Не удалось скопировать сводку в буфер обмена."] = "Could not copy the summary to the clipboard.", ["Диагностика"] = "Diagnostics",
        ["Этот компьютер будет работать как GPU-подмайнер и подключится к уже запущенной ноде на другом ПК. Локальную ноду запускать не нужно."] = "This computer will act as a GPU subminer connected to a node on another PC. No local node is needed.",
        ["ПОДКЛЮЧЕНИЕ К НОДЕ"] = "CONNECT TO NODE", ["Адрес RPC-ноды"] = "Node RPC address", ["Пароль роли майнера"] = "Miner role password",
        ["Адрес для награды"] = "Reward address", ["Видеокарта"] = "GPU", ["Подключаюсь…"] = "Connecting…", ["Подключить и начать GPU-майнинг"] = "Connect and start GPU mining",
        ["Укажите локальный порт защищённого туннеля и учётные данные miner RPC."] = "Enter the local port of the encrypted tunnel and the miner RPC credentials.",
        ["Адрес получателя"] = "Recipient address", ["Адрес GeoCoin получателя"] = "GeoCoin recipient address", ["Введите сумму больше нуля, используя точку или запятую."] = "Enter an amount greater than zero, using a dot or comma.",
        ["Сумма, GC"] = "Amount, GC", ["Например, 1,25"] = "For example, 1.25", ["Пароль кошелька"] = "Wallet password",
        ["Комиссия рассчитается автоматически. Отправленную транзакцию нельзя отменить."] = "The fee is calculated automatically. A sent transaction cannot be reversed.", ["Проверить и отправить"] = "Review and send",
        ["Выберите существующий адрес или создайте новый. Покажите его отправителю или дайте отсканировать QR-код."] = "Choose an existing address or create a new one. Share it with the sender or let them scan the QR code.",
        ["АДРЕС ДЛЯ ПОЛУЧЕНИЯ"] = "RECEIVE ADDRESS", ["Новый адрес появится здесь"] = "A new address will appear here", ["В кошельке пока нет адреса"] = "This wallet has no address yet",
        ["Создать новый адрес"] = "Create new address", ["Копировать адрес"] = "Copy address", ["QR содержит только адрес"] = "QR code contains the address only",
        ["Сохранить QR-код"] = "Save QR code", ["Импортировать QR"] = "Import QR", ["Открыть обзор"] = "Open overview",
        ["На этой странице пока нет данных."] = "There is no data on this page yet.", ["Кошелёк не открыт"] = "Wallet is not open",
        ["Чтобы показать адреса и QR-код, сначала запустите ноду. Затем создайте или откройте кошелёк."] = "Start the node to view addresses and QR codes, then create or open a wallet.",
        ["Чтобы получить монеты, создайте новый кошелёк или импортируйте существующий на странице «Обзор»."] = "To receive coins, create a wallet or import an existing one on the Overview page.",
        ["История появится после запуска ноды и подключения кошелька. Запустите ноду на странице «Обзор»."] = "History will appear after you start the node and connect a wallet. Start it on the Overview page.",
        ["Создайте или импортируйте кошелёк на странице «Обзор», чтобы загрузить историю переводов и наград."] = "Create or import a wallet on the Overview page to load transfers and mining rewards.",
        ["Для управления кошельком запустите ноду на странице «Обзор»."] = "Start the node on the Overview page to manage the wallet.",
        ["Создайте или импортируйте кошелёк на странице «Обзор», чтобы управлять резервными копиями и ключами."] = "Create or import a wallet on the Overview page to manage backups and keys.",
        ["Нода остановлена. Можно подготовить адрес и сумму; для отправки сначала запустите ноду на странице «Обзор»."] = "The node is stopped. You can prepare the address and amount; start the node on the Overview page to send.",
        ["Откройте или создайте кошелёк на странице «Обзор», чтобы отправлять монеты."] = "Open or create a wallet on the Overview page to send coins.",
        ["Введите адрес получателя или загрузите QR-код. Перед отправкой проверьте сумму и оценку комиссии."] = "Enter the recipient address or load a QR code. Check the amount and fee estimate before sending.",
        ["Рассчитываю оценку комиссии…"] = "Calculating fee estimate…", ["Комиссия появится после запуска ноды."] = "Fee estimate will appear when the node is running.",
        ["Сумма ориентировочная; GeoCoin рассчитывает комиссию по размеру транзакции."] = "This is an estimate; GeoCoin calculates the final fee from the transaction size.",
        ["Оценка комиссии рассчитана для обычной транзакции и может отличаться от точной суммы."] = "The estimate uses a typical transaction and may differ from the final fee.",
        ["Сохранить QR-код адреса"] = "Save address QR code", ["Выберите изображение с QR-кодом GeoCoin"] = "Choose an image containing a GeoCoin QR code",
        ["QR-код сохранён как изображение PNG."] = "QR code saved as a PNG image.", ["Адрес загружен из QR-кода. Перед отправкой проверьте адрес и сумму."] = "Address loaded from QR code. Check the address and amount before sending.",
        ["В выбранном изображении не найден QR-код."] = "No QR code was found in the selected image.", ["QR-код не содержит корректный адрес GeoCoin."] = "The QR code does not contain a valid GeoCoin address.",
        ["Этот QR-код содержит адрес другой сети или неверный адрес."] = "This QR code contains an invalid address or an address for another network.", ["QR-код не содержит адрес GeoCoin."] = "The QR code does not contain a GeoCoin address.",
        ["Сначала запустите ноду, чтобы рассчитать комиссию."] = "Start the node to calculate the fee.", ["Нода не сообщила минимальную комиссию."] = "The node did not provide a minimum fee.",
        ["Не удалось получить оценку комиссии."] = "Could not get a fee estimate.",
        ["Адрес создан. Его можно скопировать или отсканировать QR-код."] = "Address created. Copy it or scan the QR code.", ["Адрес скопирован. Теперь его можно отправить отправителю."] = "Address copied. You can now share it with the sender.",
        ["Загружаю адреса текущего кошелька…"] = "Loading addresses from this wallet…", ["Выберите адрес для перевода или создайте новый. QR-код содержит только адрес."] = "Choose an address or create one. The QR code contains the address only.",
        ["В кошельке нет адресов. Создайте новый адрес для получения монет."] = "This wallet has no addresses. Create one to receive coins.", ["Не удалось загрузить адреса кошелька: "] = "Could not load wallet addresses: ",
        ["История входящих переводов, отправок и наград за майнинг."] = "History of received and sent transfers and mining rewards.", ["Обновить"] = "Refresh",
        ["Дата"] = "Date", ["Операция"] = "Operation", ["Сумма"] = "Amount", ["Комиссия"] = "Fee", ["Состояние"] = "Status", ["Адрес"] = "Address",
        ["Получение"] = "Received", ["Отправка"] = "Sent", ["Награда"] = "Reward", ["Отменена"] = "Reversed", ["Ожидает зрелости"] = "Immature", ["Ожидает"] = "Pending",
        ["Выберите запись, чтобы посмотреть номер транзакции."] = "Select a record to view its transaction ID.", ["Копировать ID"] = "Copy ID",
        ["Открыть в блокчейн-обозревателе ↗"] = "Open in block explorer ↗",
        ["Не удалось открыть обозреватель. Проверьте подключение к интернету."] = "Could not open the block explorer. Check your internet connection.",
        ["Не удалось открыть обозреватель"] = "Could not open the block explorer",
        ["Обновляю историю кошелька…"] = "Updating wallet history…",
        ["Создайте или импортируйте кошелёк, чтобы увидеть историю."] = "Create or import a wallet to view its history.", ["Не удалось загрузить историю: "] = "Could not load history: ",
        ["Резервная копия сохраняет весь кошелёк. Приватный ключ относится только к одному адресу."] = "A backup saves the full wallet. A private key applies to one address only.",
        ["Полная копия кошелька"] = "Full wallet backup", ["Файл .dat включает весь текущий кошелёк и подходит для его восстановления."] = "The .dat file contains the whole wallet and can restore it.",
        ["Приватный ключ одного адреса"] = "Private key for one address", ["В GeoCoin нет мнемонической фразы. Приватный ключ из 64 hex-символов открывает один адрес."] = "GeoCoin has no recovery phrase. A 64-character hexadecimal private key unlocks one address.",
        ["Адрес для экспорта ключа"] = "Address to export", ["Сохранить ключ выбранного адреса"] = "Save key for selected address", ["Импорт ключа в текущий кошелёк"] = "Import key into this wallet",
        ["64 шестнадцатеричных символа"] = "64 hexadecimal characters", ["Импортировать ключ"] = "Import key",
        ["Ключи — секретные данные. Не отправляйте их другим людям и не храните в общедоступных файлах."] = "Keys are secret. Do not share them or store them in public files.",
        ["Синхронизация завершена"] = "Synchronization complete", ["Нода остановлена"] = "Node stopped", ["Нода не запущена"] = "Node is not running",
        ["Синхронизация"] = "Synchronizing", ["Нода завершилась"] = "Node exited", ["Нода запускается…"] = "Node is starting…", ["Подключаю GPU к удалённой ноде…"] = "Connecting GPU to remote node…",
        ["Подключаюсь к сети GeoCoin…"] = "Connecting to the GeoCoin network…",
        ["Локальная нода остановлена · работает режим подмайнера"] = "Local node stopped · subminer is running", ["RPC отвечает"] = "RPC is responding",
        ["Процесс запущен · проверяю RPC…"] = "Process started · checking RPC…", ["Остановите подмайнер, чтобы сменить протокол GPU"] = "Stop the subminer to change the GPU protocol",
        ["ЖУРНАЛ RPC ПОДМАЙНЕРА"] = "SUBMINER RPC LOG",
        ["Попыток подключения ещё не было."] = "No connection attempts yet.",
        ["Проверяю RPC"] = "Checking RPC",
        ["Нода ответила на проверочный запрос"] = "Node answered the probe",
        ["Получен шаблон блока"] = "Block template received",
        ["Подмайнер завершил работу"] = "Subminer exited",
        ["Подмайнер повторяет запрос RPC"] = "Subminer is retrying RPC",
        ["Нода приняла блок"] = "Node accepted the block",
        ["Нода отклонила блок"] = "Node rejected the block",
        ["Подмайнер сообщил об ошибке"] = "Subminer reported an error",
        ["Нет HTTP-ответа от RPC. Проверьте туннель и запустите ноду."] = "No HTTP response from RPC. Check the tunnel and start the node.",
        ["Истекло время ожидания RPC."] = "RPC request timed out.",
        ["RPC отклонил заголовок Host. Используйте порт 3033 на обеих сторонах SSH-туннеля."] = "RPC rejected the Host header. Use port 3033 at both ends of the SSH tunnel.",
        ["Подмайнер подключился к RPC"] = "Subminer connected to RPC", ["Ожидаю первый ответ RPC подмайнера…"] = "Waiting for the subminer's first RPC response…", ["Предыдущая страница"] = "Previous page", ["Следующая страница"] = "Next page", ["Подключение к ноде изменилось. Проверьте перевод заново."] = "The node connection changed. Review the transfer again.", ["RPC не запущен"] = "RPC is not running", ["Сеть: —"] = "Network: —",
        ["Не удалось проверить видеокарту."] = "Could not check the GPU.", ["GPU-майнер не найден. Переустановите GeoCoin-Start."] = "GPU miner not found. Reinstall GeoCoin-Start.",
        ["GPU-майнинг работает"] = "GPU mining is running", ["GPU-подмайнер остановлен."] = "GPU subminer stopped.", ["GPU-майнинг остановлен."] = "GPU mining stopped.",
        ["Не удалось запустить GPU-майнинг: "] = "Could not start GPU mining: ", ["Не удалось подключиться: "] = "Could not connect: ",
        ["Проверяю miner RPC и адрес награды…"] = "Checking miner RPC and reward address…", ["Подготавливаю подключение к удалённой ноде…"] = "Preparing connection to remote node…",
        ["Не удалось загрузить адреса: "] = "Could not load addresses: ", ["Не удалось импортировать ключ: "] = "Could not import key: ",
        ["Не удалось экспортировать ключ: "] = "Could not export key: ", ["В кошельке нет доступных адресов."] = "No available addresses in this wallet.",
        ["Ключ добавлен. История адреса восстановлена."] = "Key imported. Address history restored.",
        ["Адреса появятся после запуска ноды и открытия кошелька."] = "Addresses will appear after the node starts and the wallet is opened.",
        ["Совместимая видеокарта не найдена. Проверьте драйверы."] = "No compatible GPU found. Check the drivers.",
        ["Кошелёк создан. Сейчас сделайте резервную копию на странице «Обзор»."] = "Wallet created. Make a backup on the Overview page now.",
        ["Проверьте ключ: нужно ровно 64 шестнадцатеричных символа 0–9, A–F."] = "Check the key: it must contain exactly 64 hexadecimal characters, 0–9 or A–F.",
        ["Ключ сохранён в выбранный файл. Храните его отдельно и никому не передавайте."] = "Key saved to the selected file. Store it separately and do not share it.",
        ["Ключ сохранён"] = "Key saved", ["Не удалось создать адрес"] = "Could not create address",
        ["В истории пока нет операций. После синхронизации обновите список."] = "No transactions yet. Refresh the list after synchronization.",
        ["Проверьте адрес получателя."] = "Check the recipient address.", ["Сумма может содержать не больше 8 знаков после запятой."] = "The amount can have no more than 8 decimal places.",
        ["Введите пароль кошелька."] = "Enter the wallet password.", ["Отправляю перевод…"] = "Sending transfer…",
        ["ID транзакции скопирован."] = "Transaction ID copied.", ["Не удалось скопировать ID: "] = "Could not copy ID: ",
        ["Сегодня: "] = "Today: ", ["Текущая сессия: "] = "Current session: ", ["Последняя сессия: "] = "Last session: ", ["Сессия: "] = "Session: ",
        ["Блоков: "] = "Blocks: ", ["Узлов сети: "] = "Network peers: ", ["подключений: "] = "connections: ", ["Скорость: "] = "Hashrate: ",
        ["Ожидает данных…"] = "Waiting for data…", ["Остановить подмайнер"] = "Stop subminer", ["Не удалось создать адрес: "] = "Could not create address: ",
        ["Адрес скопирован"] = "Address copied", ["Зрелые монеты"] = "Mature coins", ["Незрелые монеты"] = "Immature coins"
    };
    private static readonly Dictionary<string, string> EnglishToRussian = RussianToEnglish
        .GroupBy(entry => entry.Value, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);

    private readonly TableLayoutPanel _shell = new();
    private readonly Panel _navigationRail = new();
    private readonly TableLayoutPanel _railLayout = new();
    private readonly Panel _brandPanel = new();
    private readonly ThemeAwareFlowLayoutPanel _navigationList = new();
    private readonly Panel _railFooter = new();
    private readonly PictureBox _brandIcon = new();
    private readonly Label _brandName = new();
    private readonly Label _brandSubtitle = new();
    private readonly ThemeAwarePanel _pagePanel = new();
    private readonly Panel _headerPanel = new();
    private readonly Label _pageTitle = new();
    private readonly Label _pageSubtitle = new();
    private readonly Label _statusLabel = new();
    private readonly ThemeAwareComboBox _languageCombo = new();
    private readonly Button _themeButton = new();
    private readonly Button _designButton = new();
    private readonly Button _checkUpdatesButton = new();
    private readonly Button _securityButton = new();
    private readonly Panel _lockOverlay = new();
    private readonly Label _lockBalance = new();
    private readonly Label _lockBlocks = new();
    private readonly Label _lockHashrate = new();
    private readonly Label _lockNodeStatus = new();
    private readonly Label _lockMessage = new();
    private readonly TextBox _lockPinInput = new();
    private readonly Button _lockUnlockButton = new();
    private readonly Button _lockHelloButton = new();
    private readonly System.Windows.Forms.Timer _idleLockTimer = new() { Interval = 15_000 };
    private readonly ToolTip _headerToolTip = new();
    private readonly HashSet<Button> _primaryButtons = new();
    private readonly HashSet<Button> _secondaryButtons = new();
    private UserActivityMessageFilter? _activityMessageFilter;
    private NotifyIcon? _systemNotificationIcon;
    private ContextMenuStrip? _notificationMenu;
    private ToolStripItem? _trayOpenMenuItem;
    private ToolStripItem? _trayExitMenuItem;
    private DateTimeOffset _lastUiActivityUtc = DateTimeOffset.UtcNow;
    private bool _applicationLocked;
    private bool _notificationBaselineReady;
    private bool _networkFailureNotified;
    private int _pinFailedAttempts;
    private DateTimeOffset? _pinLockoutUntil;
    private readonly Label _balanceValue = new();
    private readonly Label _walletAddress = new();
    private readonly Label _nodeState = new();
    private readonly Label _syncState = new();
    private readonly Label _peerState = new();
    private readonly Label _miningState = new();
    private readonly Label _cpuHashrate = new();
    private readonly Label _gpuMiningState = new();
    private readonly Label _miningHashrate = new();
    private readonly Label _gpuRewardAddress = new();
    private readonly Label _transactionStatus = new();
    private readonly Label _transactionDetails = new();
    private readonly Label _walletKeyStatus = new();
    private readonly Label _nodeProcessStatus = new();
    private readonly Label _nodeRpcStatus = new();
    private readonly Label _nodeRuntimeStatus = new();
    private readonly Label _nodePeerStatus = new();
    private readonly Label _nodePeerSummary = new();
    private readonly Label _nodePeerHint = new();
    private readonly TextBox _nodePeerDetails = new();
    private readonly Button _nodePeerToggleButton = new();
    private readonly LinkLabel _nodeP2pHelpLink = new();
    private readonly ToolTip _nodePeerToolTip = new() { AutoPopDelay = 15000, InitialDelay = 450, ReshowDelay = 100, ShowAlways = true };
    private Panel? _nodePeerCard;
    private Panel? _nodeRpcCard;
    private Panel? _nodeLogCard;
    private readonly List<PeerDiagnostic> _nodePeerDiagnostics = new();
    private DateTime _lastPeerDiagnosticsRefreshUtc = DateTime.MinValue;
    private bool _peerDiagnosticsLoading;
    private bool _peerDiagnosticsAvailable;
    private bool _peerListAvailable;
    private bool _nodePeersExpanded;
    private readonly Label _nodeProfileHint = new();
    private readonly Label _minedBlocksTodayLabel = new();
    private readonly Label _minedBlocksSessionLabel = new();
    private readonly Label _lifetimeMinedBlocksLabel = new();
    private readonly Label _matureCoinsLabel = new();
    private readonly Label _immatureCoinsLabel = new();
    private readonly TextBox _nodeLogBox = new();
    private readonly TextBox _subminerRpcLogBox = new();
    private readonly Queue<string> _subminerRpcLog = new();

    private readonly ThemeAwareNumericUpDown _threadCount = new();
    private readonly ThemeAwareComboBox _gpuBackendCombo = new();
    private readonly ThemeAwareComboBox _remoteGpuBackendCombo = new();
    private readonly PageWheelCheckedListBox _gpuDeviceList = new();
    private readonly PageWheelCheckedListBox _remoteGpuDeviceList = new();
    private readonly HashSet<string> _selectedGpuDeviceKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _initializedGpuBackends = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedGpuBackend;
    private bool _updatingGpuDeviceLists;
    private readonly ThemeAwareComboBox _rpcBindCombo = new();
    private readonly ThemeAwareComboBox _walletAddressCombo = new();
    private readonly ThemeAwareComboBox _receiveAddressCombo = new();
    private readonly PictureBox _receiveQrCode = new();
    private readonly Button _refreshGpuButton = new();
    private readonly Button _refreshTransactionsButton = new();
    private readonly Button _copyTransactionIdButton = new();
    private readonly LinkLabel _openTransactionExplorerLink = new();
    private readonly Button _previousTransactionsPageButton = new();
    private readonly Button _nextTransactionsPageButton = new();
    private readonly ThemeAwareComboBox _transactionsPageSizeCombo = new();
    private readonly Label _transactionsPageLabel = new();
    private readonly Label _transactionsPageSizeLabel = new();
    private readonly Button _walletFileExportButton = new();
    private readonly Button _walletPasswordChangeButton = new();
    private readonly Button _walletKeyExportButton = new();
    private readonly Button _walletKeyImportButton = new();
    private readonly Button _refreshWalletAddressesButton = new();
    private readonly Button _nodeProfileCopyButton = new();
    private readonly Button _nodeDiagnosticsCopyButton = new();
    private readonly Button _nodeLogOpenButton = new();
    private readonly CheckBox _pauseNodeLogUpdates = new();
    private readonly Button _remoteGpuButton = new();
    private readonly Button _remoteGpuFindButton = new();
    private readonly TextBox _sendAddress = new();
    private readonly TextBox _sendAmount = new();
    private readonly TextBox _sendPassword = new();
    private readonly TextBox _privateKeyInput = new();
    private readonly TextBox _minerRpcUserInput = new() { Text = "geocoin-start-miner" };
    private readonly TextBox _minerRpcPasswordInput = new() { Text = RandomSecret(), UseSystemPasswordChar = true };
    private readonly TextBox _remoteNodeUrlInput = new() { Text = "http://127.0.0.1:3033/" };
    private readonly TextBox _remoteMinerUserInput = new() { Text = "geocoin-start-miner" };
    private readonly TextBox _remoteMinerPasswordInput = new() { UseSystemPasswordChar = true };
    private readonly TextBox _remoteRewardAddressInput = new();
    private readonly Button _toggleMinerPasswordVisibilityButton = new();
    private readonly Button _toggleRemotePasswordVisibilityButton = new();
    private readonly Label _sendMessage = new();
    private readonly Label _receiveAddress = new();
    private readonly Label _receiveMessage = new();
    private readonly Label _receiveQrCaption = new();
    private readonly Label _sendFeeEstimate = new();
    private readonly Label _sendFeeHint = new();
    private readonly Label _overviewHint = new();
    private readonly Button _nodeButton = new();
    private readonly Button _walletButton = new();
    private readonly Button _walletRestoreButton = new();
    private readonly Button _mineButton = new();
    private readonly Button _gpuMineButton = new();
    private readonly Button _sendButton = new();
    private readonly Button _receiveButton = new();
    private readonly Button _receiveCopyButton = new();
    private readonly Button _saveReceiveQrButton = new();
    private readonly Button _importPaymentQrButton = new();
    private readonly Button[] _navigation = new Button[10];
    private readonly Label _pinSettingsStatus = new();
    private readonly ThemeAwareComboBox _pinLengthCombo = new();
    private readonly TextBox _currentPinInput = new() { UseSystemPasswordChar = true, MaxLength = 6 };
    private readonly TextBox _newPinInput = new() { UseSystemPasswordChar = true, MaxLength = 6 };
    private readonly TextBox _confirmPinInput = new() { UseSystemPasswordChar = true, MaxLength = 6 };
    private readonly CheckBox _autoLockCheck = new();
    private readonly ThemeAwareNumericUpDown _autoLockMinutesInput = new() { Minimum = 1, Maximum = 120, Value = 10, Width = 72 };
    private readonly Label _autoLockUnitsLabel = new();
    private readonly CheckBox _windowsHelloCheck = new();
    private readonly CheckBox _notificationsCheck = new();
    private readonly CheckBox _notifyNodeStoppedCheck = new();
    private readonly CheckBox _notifySubminerRpcCheck = new();
    private readonly CheckBox _notifySyncCheck = new();
    private readonly CheckBox _notifyMiningCheck = new();
    private readonly CheckBox _notifyIncomingTransfersCheck = new();
    private readonly CheckBox _notifyOutgoingTransfersCheck = new();
    private readonly CheckBox _autoStartCheck = new();
    private readonly Button _saveSecurityButton = new();
    private readonly Button _saveNotificationsButton = new();
    private readonly Button _testNotificationButton = new();
    private readonly Button _disablePinButton = new();
    private readonly Button _lockNowButton = new();
    private readonly Button _reopenGuideButton = new();
    private readonly Button _guideActionButton = new();
    private readonly Button _guideSecondaryButton = new();
    private readonly Button _guideDoneButton = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 4000 };
    private readonly System.Windows.Forms.Timer _sendFeeRefreshTimer = new() { Interval = 450 };
    private readonly object _minedBlocksLock = new();
    private readonly List<MinedBlockRecord> _minedBlockRecords = new();
    private readonly HashSet<string> _miningSessionBlockIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _lifetimeMinedBlockIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _minedBlockIdByTransactionId = new(StringComparer.OrdinalIgnoreCase);

    private GeoCoinRpcClient? _rpc;
    private GeoCoinRpcClient? _minerRpc;
    private DataGridView? _transactionGrid;
    private List<JsonElement> _transactionEntries = new();
    private int _transactionsPageSize = 15;
    private int _transactionsPageIndex;
    private Process? _nodeProcess;
    private Process? _gpuMinerProcess;
    private WindowsAsciiPathAlias? _nodePathAlias;
    private StreamWriter? _logWriter;
    private string? _adminPassword;
    private string? _minerPassword;
    private string _minerRpcUsername = "geocoin-start-miner";
    private string _rpcBindAddress = "127.0.0.1";
    private string? _sessionDirectory;
    private string? _minerPassFile;
    private string? _remoteMinerPassFile;
    private string? _lastAddress;
    private string _currentPage = "overview";
    private string _nodeStatus = "Нода остановлена";
    private decimal _balance;
    private decimal _matureCoins;
    private decimal _immatureCoins;
    private long _height;
    private long _headers;
    private int _connections;
    private bool _cpuMiningActive;
    private bool _gpuMiningActive;
    private bool _remoteGpuMining;
    private bool _startingNode;
    private bool _startingRemoteGpu;
    private bool _rpcHealthy;
    private bool _miningSessionActive;
    private DateTimeOffset? _miningSessionStartedAt;
    private DateTimeOffset? _miningSessionEndedAt;
    private DateTimeOffset _lastWalletMiningStatsPollUtc = DateTimeOffset.MinValue;
    private bool _checkingGpuDevices;
    private bool _gpuDevicesScanned;
    private bool _loadingTransactions;
    private bool _transactionsAutoLoadPending;
    private string? _updateInstallerToLaunch;
    private bool _loadingWalletAddresses;
    private bool _loadingReceiveAddresses;
    private bool _walletHistoryBackfilled;
    private double _cpuHashrateValue;
    private string _gpuHashrateText = "Скорость: —";
    private string _gpuStatusText = "GPU-майнинг остановлен";
    private string? _gpuLastError;
    private readonly List<GpuDevice> _gpuDevices = new();
    private readonly List<string> _walletAddresses = new();
    private string? _selectedTransactionId;
    private bool _refreshing;
    private bool _allowClose;
    private bool _trayExitRequested;
    private bool _closePromptOpen;
    private bool _darkTheme;
    private bool _modernDesign;
    private bool _modernNavigationLayoutQueued;
    private string _uiLanguage = "ru";
    private bool _firstRunGuideCompleted;
    private bool _firstRunBackupConfirmed;
    private bool _notificationsEnabled = true;
    private bool _notifyNodeStopped = true;
    private bool _notifySubminerRpc = true;
    private bool _notifySyncComplete = true;
    private bool _notifyMiningRewards = true;
    private bool _notifyIncomingTransfers = true;
    private bool _notifyOutgoingTransfers = true;
    private bool _autoStartEnabled;
    private bool _updatingAutoStartCheck;
    private bool _incomingTransferBaselineReady;
    private HashSet<string> _knownIncomingTransferIds = new(StringComparer.OrdinalIgnoreCase);
    private bool _autoLockEnabled = true;
    private int _autoLockMinutes = 10;
    private bool _buildingSettingsPage;
    private bool _windowsHelloEnabled;
    private string? _protectedPinVerifier;
    private bool _refreshingSendFee;
    private int _lastPageViewportWidth;
    private int _selectedThreads = Math.Max(1, Environment.ProcessorCount / 2);

    public MainForm()
    {
        LoadUiPreferences();
        try { _autoStartEnabled = StartupRegistration.IsEnabled(); } catch { }
        _activityMessageFilter = new UserActivityMessageFilter(() => _lastUiActivityUtc = DateTimeOffset.UtcNow);
        Application.AddMessageFilter(_activityMessageFilter);
        Text = "GeoCoin-Start — простой кошелёк";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "") ?? SystemIcons.Application;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        MinimumSize = new Size(940, 640);
        Size = new Size(1120, 760);
        BackColor = Canvas;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 10);
        FormClosing += HandleClosing;
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized && Visible)
                HideToSystemTray();
        };
        FormClosed += (_, _) =>
        {
            _idleLockTimer.Stop();
            if (_activityMessageFilter is not null) Application.RemoveMessageFilter(_activityMessageFilter);
            _systemNotificationIcon?.Dispose();
            _systemNotificationIcon = null;
            _notificationMenu?.Dispose();
            _notificationMenu = null;
            _nodePathAlias?.Dispose();
            _nodePathAlias = null;
            _nodePeerToolTip.Dispose();
            if (_updateInstallerToLaunch is not { } installerPath) return;
            _updateInstallerToLaunch = null;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    WorkingDirectory = Path.GetDirectoryName(installerPath)!,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                try { File.Delete(installerPath); } catch { }
                ShowLocalizedMessage("Не удалось запустить установщик обновления: " + ex.Message,
                    "Ошибка обновления", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        LoadMinedBlockRecords();
        BuildShell();
        ApplyThemeToTree(this);
        InitializeSystemNotifications();
        _nodeButton.Click += async (_, _) =>
        {
            if (IsNodeRunning()) await StopNodeAsync();
            else await StartNodeAsync();
            BuildPage();
        };
        _nodeProfileCopyButton.Click += (_, _) => CopyMinerConnectionProfile();
        _nodeDiagnosticsCopyButton.Click += (_, _) => CopyNodeDiagnostics();
        _nodeLogOpenButton.Click += (_, _) => OpenNodeLog();
        _toggleMinerPasswordVisibilityButton.Click += (_, _) =>
        {
            _minerRpcPasswordInput.UseSystemPasswordChar = !_minerRpcPasswordInput.UseSystemPasswordChar;
            _toggleMinerPasswordVisibilityButton.Text = _minerRpcPasswordInput.UseSystemPasswordChar ? "Показать" : "Скрыть";
        };
        _toggleRemotePasswordVisibilityButton.Click += (_, _) =>
        {
            _remoteMinerPasswordInput.UseSystemPasswordChar = !_remoteMinerPasswordInput.UseSystemPasswordChar;
            _toggleRemotePasswordVisibilityButton.Text = _remoteMinerPasswordInput.UseSystemPasswordChar ? "Показать" : "Скрыть";
        };
        _walletButton.Click += async (_, _) =>
        {
            if (File.Exists(AppPaths.WalletPath)) await BackupWalletAsync();
            else await CreateWalletAsync();
            BuildPage();
        };
        _walletRestoreButton.Click += async (_, _) =>
        {
            await ImportWalletAsync();
            BuildPage();
        };
        _guideActionButton.Click += async (_, _) =>
        {
            int step = GetFirstRunStep();
            if (_firstRunGuideCompleted)
            {
                SetPage("overview");
                return;
            }
            if (step == 4)
            {
                _firstRunGuideCompleted = true;
                SaveUiPreferences();
                BuildPage();
                return;
            }
            switch (GetFirstRunStep())
            {
                case 1: await StartNodeAsync(offerWalletCreation: false); BuildPage(); break;
                case 2: _walletButton.PerformClick(); break;
                case 3: SetPage("wallet"); break;
                default: SetPage("mining"); break;
            }
        };
        _guideSecondaryButton.Click += async (_, _) =>
        {
            switch (GetFirstRunStep())
            {
                case 1: SetPage("node"); break;
                case 2: _walletRestoreButton.PerformClick(); break;
                case 3:
                    await BackupWalletAsync();
                    break;
                default: SetPage("receive"); break;
            }
        };
        _guideDoneButton.Click += (_, _) =>
        {
            if (GetFirstRunStep() == 3)
            {
                _firstRunBackupConfirmed = true;
                SaveUiPreferences();
                BuildPage();
                return;
            }
            // Skipping only closes this page. Completion is saved only after the user finishes step 4.
            SetPage("overview");
        };
        _reopenGuideButton.Click += (_, _) =>
        {
            SetPage("guide");
        };
        _saveSecurityButton.Click += (_, _) => SaveSecuritySettingsFromUi();
        _saveNotificationsButton.Click += (_, _) => SaveNotificationSettings();
        _autoStartCheck.CheckedChanged += (_, _) => SaveAutoStartPreference();
        _autoLockCheck.CheckedChanged += (_, _) =>
        {
            _autoLockMinutesInput.Enabled = _autoLockCheck.Checked;
            _autoLockUnitsLabel.Enabled = _autoLockCheck.Checked;
            if (_buildingSettingsPage) return;
            _autoLockEnabled = _autoLockCheck.Checked;
            SaveUiPreferences();
        };
        _autoLockMinutesInput.ValueChanged += (_, _) =>
        {
            if (_buildingSettingsPage) return;
            _autoLockMinutes = (int)_autoLockMinutesInput.Value;
            SaveUiPreferences();
        };
        _windowsHelloCheck.CheckedChanged += (_, _) =>
        {
            if (_buildingSettingsPage || !IsPinLockConfigured()) return;
            _windowsHelloEnabled = _windowsHelloCheck.Checked;
            SaveUiPreferences();
        };
        _testNotificationButton.Click += (_, _) =>
        {
            if (!_notificationsEnabled)
            {
                ShowLocalizedMessage("Включите и сохраните системные уведомления, чтобы отправить тест.",
                    "Уведомления выключены", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!ShowSystemNotification("Тестовое уведомление GeoCoin-Start", "Системные уведомления приложения включены."))
                ShowLocalizedMessage("Windows заблокировала системные уведомления приложения. Проверьте настройки уведомлений Windows.",
                    "Проверка уведомлений", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        _disablePinButton.Click += (_, _) => DisablePinFromSettings();
        _lockNowButton.Click += (_, _) => LockApplication();
        _lockHelloButton.Click += async (_, _) => await UnlockWithWindowsHelloAsync();
        _notificationsCheck.CheckedChanged += (_, _) =>
        {
            bool enabled = _notificationsCheck.Checked;
            _notifyNodeStoppedCheck.Enabled = enabled;
            _notifySubminerRpcCheck.Enabled = enabled;
            _notifySyncCheck.Enabled = enabled;
            _notifyMiningCheck.Enabled = enabled;
        };
        foreach (TextBox pinInput in new[] { _currentPinInput, _newPinInput, _confirmPinInput })
            pinInput.KeyPress += (_, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
            };
        _mineButton.Click += async (_, _) => await ToggleCpuMiningAsync();
        _gpuMineButton.Click += async (_, _) => await ToggleGpuMiningAsync();
        _refreshGpuButton.Click += async (_, _) => await RefreshGpuDevicesAsync();
        _remoteGpuFindButton.Click += async (_, _) => await RefreshGpuDevicesAsync();
        _remoteGpuButton.Click += async (_, _) => await ToggleRemoteGpuMiningAsync();
        _gpuDeviceList.ForwardWheelToPage = ScrollPageWithMouseWheel;
        _remoteGpuDeviceList.ForwardWheelToPage = ScrollPageWithMouseWheel;
        _gpuBackendCombo.SelectedIndexChanged += (_, _) => GpuBackendChanged(_gpuBackendCombo, _gpuDeviceList);
        _remoteGpuBackendCombo.SelectedIndexChanged += (_, _) => GpuBackendChanged(_remoteGpuBackendCombo, _remoteGpuDeviceList);
        _gpuBackendCombo.SelectionChangeCommitted += (_, _) => GpuBackendChanged(_gpuBackendCombo, _gpuDeviceList);
        _remoteGpuBackendCombo.SelectionChangeCommitted += (_, _) => GpuBackendChanged(_remoteGpuBackendCombo, _remoteGpuDeviceList);
        _gpuDeviceList.ItemCheck += (_, e) => GpuDeviceItemCheck(_gpuDeviceList, e);
        _remoteGpuDeviceList.ItemCheck += (_, e) => GpuDeviceItemCheck(_remoteGpuDeviceList, e);
        _walletAddressCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_walletKeyExportButton.IsHandleCreated)
                _walletKeyExportButton.Enabled = IsNodeRunning() && _walletAddressCombo.SelectedItem is string;
        };
        _receiveAddressCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingReceiveAddresses || _receiveAddressCombo.SelectedItem is not string address) return;
            _lastAddress = address;
            UpdateReceiveAddressDisplay();
        };
        _sendButton.Click += async (_, _) => await SendAsync();
        _receiveButton.Click += async (_, _) => await CreateReceiveAddressAsync();
        _receiveCopyButton.Click += (_, _) => CopyReceiveAddress();
        _saveReceiveQrButton.Click += (_, _) => SaveReceiveQrCode();
        _importPaymentQrButton.Click += (_, _) => ImportPaymentQrFromImage();
        _sendAmount.TextChanged += (_, _) =>
        {
            _sendFeeRefreshTimer.Stop();
            _sendFeeRefreshTimer.Start();
        };
        _sendFeeRefreshTimer.Tick += async (_, _) =>
        {
            _sendFeeRefreshTimer.Stop();
            await RefreshSendFeeEstimateAsync();
        };
        _refreshTransactionsButton.Click += async (_, _) => await RefreshTransactionsAsync();
        _previousTransactionsPageButton.Click += (_, _) =>
        {
            if (_transactionsPageIndex == 0) return;
            _transactionsPageIndex--;
            RenderTransactionsPage();
        };
        _nextTransactionsPageButton.Click += (_, _) =>
        {
            if (_transactionsPageIndex + 1 >= GetTransactionPageCount()) return;
            _transactionsPageIndex++;
            RenderTransactionsPage();
        };
        _transactionsPageSizeCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_transactionsPageSizeCombo.SelectedItem is not int pageSize) return;
            _transactionsPageSize = pageSize;
            _transactionsPageIndex = 0;
            RenderTransactionsPage();
        };
        _copyTransactionIdButton.Click += (_, _) => CopySelectedTransactionId();
        _walletFileExportButton.Click += async (_, _) => await BackupWalletAsync();
        _walletPasswordChangeButton.Click += async (_, _) => await ChangeWalletPasswordAsync();
        _walletKeyExportButton.Click += async (_, _) => await ExportPrivateKeyAsync();
        _walletKeyImportButton.Click += async (_, _) => await ImportPrivateKeyAsync();
        _refreshWalletAddressesButton.Click += async (_, _) => await RefreshWalletAddressesAsync();
        _threadCount.ValueChanged += (_, _) => _selectedThreads = (int)_threadCount.Value;
        _refreshTimer.Tick += async (_, _) =>
        {
            await RefreshStateAsync();
            if (_currentPage == "node") UpdateNodePage();
            UpdateMinedBlockCounters();
            ApplyUiLanguage(this);
        };
        _refreshTimer.Start();
        _idleLockTimer.Tick += (_, _) =>
        {
            if (_autoLockEnabled && IsPinLockConfigured() && !_applicationLocked &&
                DateTimeOffset.UtcNow - _lastUiActivityUtc >= TimeSpan.FromMinutes(_autoLockMinutes))
                LockApplication();
            if (_applicationLocked) UpdateLockScreenMetrics();
        };
        _idleLockTimer.Start();
        Shown += (_, _) =>
        {
            FitWindowToScreen();
            QueueModernNavigationLayout();
            if (IsPinLockConfigured()) BeginInvoke((Action)LockApplication);
        };
        DpiChanged += (_, _) =>
        {
            ApplyDesignLayout();
            LayoutHeaderStatus();
            BuildPage();
        };
        SetPage(_firstRunGuideCompleted ? "overview" : "guide");
        UpdateStatus();
    }

    private void BuildShell()
    {
        _shell.Dock = DockStyle.Fill;
        _shell.Margin = Padding.Empty;
        _shell.Padding = Padding.Empty;
        _shell.BorderStyle = BorderStyle.None;
        _shell.ColumnCount = 2;
        _shell.RowCount = 2;
        _shell.BackColor = Canvas;
        _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleUi(224)));
        _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _shell.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleUi(100)));
        _shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(_shell);

        _navigationRail.Name = "navigationRail";
        _navigationRail.Dock = DockStyle.Fill;
        _navigationRail.Margin = Padding.Empty;
        _navigationRail.BorderStyle = BorderStyle.None;
        _navigationRail.BackColor = Rail;

        _railLayout.Dock = DockStyle.Fill;
        _railLayout.Margin = Padding.Empty;
        _railLayout.Padding = Padding.Empty;
        _railLayout.BorderStyle = BorderStyle.None;
        _railLayout.ColumnCount = 1;
        _railLayout.RowCount = 3;
        _railLayout.BackColor = Rail;
        _railLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _railLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleUi(112)));
        _railLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _railLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleUi(62)));
        _navigationRail.Controls.Add(_railLayout);

        _brandPanel.Dock = DockStyle.Fill;
        _brandPanel.Margin = Padding.Empty;
        _brandPanel.BackColor = Rail;
        _railLayout.Controls.Add(_brandPanel, 0, 0);

        _brandIcon.Location = UiPoint(20, 23);
        _brandIcon.Size = UiSize(38, 38);
        _brandIcon.SizeMode = PictureBoxSizeMode.Zoom;
        _brandIcon.Image = (Icon ?? SystemIcons.Application).ToBitmap();
        _brandPanel.Controls.Add(_brandIcon);
        _brandName.Name = "brandName";
        _brandName.Text = "GeoCoin";
        _brandName.Location = UiPoint(64, 22);
        _brandName.AutoSize = true;
        _brandName.Font = new Font("Segoe UI Semibold", 22, FontStyle.Bold);
        _brandName.ForeColor = Color.FromArgb(233, 208, 153);
        _brandPanel.Controls.Add(_brandName);
        _brandSubtitle.Name = "brandSubtitle";
        _brandSubtitle.Text = "ПРОСТОЙ КОШЕЛЁК";
        _brandSubtitle.Location = UiPoint(27, 70);
        _brandSubtitle.AutoSize = true;
        _brandSubtitle.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        _brandSubtitle.ForeColor = Color.FromArgb(144, 161, 145);
        _brandPanel.Controls.Add(_brandSubtitle);

        _navigationList.Name = "navigationList";
        _navigationList.Dock = DockStyle.Fill;
        _navigationList.Margin = Padding.Empty;
        _navigationList.Padding = UiPadding(18, 8, 18, 8);
        _navigationList.AutoScroll = true;
        _navigationList.WrapContents = false;
        _navigationList.FlowDirection = FlowDirection.TopDown;
        _navigationList.BackColor = Rail;
        _navigationList.SizeChanged += (_, _) => QueueModernNavigationLayout();
        _shell.SizeChanged += (_, _) => QueueModernNavigationLayout();
        _railLayout.Controls.Add(_navigationList, 0, 1);

        string[] names = { "Обзор", "Майнинг", "Подмайнер", "Нода", "Отправить", "Получить", "История", "Кошелёк", "Помощник", "Настройки" };
        string[] pages = { "overview", "mining", "subminer", "node", "send", "receive", "transactions", "wallet", "guide", "settings" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var button = new Button
            {
                Text = names[i],
                Tag = pages[i],
                TextAlign = ContentAlignment.MiddleLeft,
                Size = UiSize(188, 38),
                Margin = Padding.Empty,
                FlatStyle = FlatStyle.Flat,
                BackColor = Rail,
                ForeColor = Color.FromArgb(206, 215, 205),
                Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Padding = new Padding(14, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(36, 50, 41);
            button.Click += (_, _) => SetPage(pages[index]);
            _navigationList.Controls.Add(button);
            _navigation[i] = button;
        }

        _railFooter.Name = "railFooter";
        _railFooter.Dock = DockStyle.Fill;
        _railFooter.BackColor = Rail;
        _railFooter.BorderStyle = BorderStyle.None;
        _railLayout.Controls.Add(_railFooter, 0, 2);

        var originalLink = new LinkLabel
        {
            Text = "Оригинальный проект ↗",
            AutoSize = false,
            Location = UiPoint(25, 4),
            Size = UiSize(190, 22),
            LinkColor = Color.FromArgb(202, 180, 132),
            ActiveLinkColor = Color.White,
            Font = new Font("Segoe UI", 8.5f)
        };
        originalLink.LinkClicked += (_, _) => OpenLink(AppPaths.OriginalRepositoryUrl);
        _railFooter.Controls.Add(originalLink);

        var distributorLink = new LinkLabel
        {
            Text = "Редистрибьютор: TheTimkol ↗",
            AutoSize = false,
            Location = UiPoint(25, 29),
            Size = UiSize(190, 22),
            LinkColor = Color.FromArgb(202, 180, 132),
            ActiveLinkColor = Color.White,
            Font = new Font("Segoe UI", 8.5f)
        };
        distributorLink.LinkClicked += (_, _) => OpenLink(AppPaths.DistributorUrl);
        _railFooter.Controls.Add(distributorLink);

        _headerPanel.Dock = DockStyle.Fill;
        _headerPanel.Margin = Padding.Empty;
        _headerPanel.BackColor = Canvas;
        _headerPanel.Controls.Add(_pageTitle);
        _pageTitle.Location = UiPoint(34, 20);
        _pageTitle.AutoSize = true;
        _pageTitle.Font = new Font("Segoe UI Semibold", 21, FontStyle.Bold);
        _pageTitle.ForeColor = Ink;
        _pageSubtitle.Text = "Управление GeoCoin без лишних настроек";
        _pageSubtitle.Location = UiPoint(37, 61);
        _pageSubtitle.AutoSize = false;
        _pageSubtitle.AutoEllipsis = true;
        _pageSubtitle.ForeColor = Muted;
        _pageSubtitle.Font = new Font("Segoe UI", 9.5f);
        _headerPanel.Controls.Add(_pageSubtitle);

        _languageCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _languageCombo.Items.AddRange(UiLocalization.LanguageNames.Cast<object>().ToArray());
        _languageCombo.SelectedIndex = Math.Max(0, Array.IndexOf(UiLocalization.LanguageCodes, _uiLanguage));
        _languageCombo.Font = new Font("Segoe UI", 9);
        _languageCombo.Size = UiSize(112, 28);
        _headerPanel.Controls.Add(_languageCombo);
        _languageCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_languageCombo.SelectedIndex < 0) return;
            string language = UiLocalization.LanguageCodes[_languageCombo.SelectedIndex];
            if (_uiLanguage == language) return;
            _uiLanguage = language;
            SaveUiPreferences();
            BuildPage();
            UpdatePreferenceControlText();
            ApplyUiLanguage(this);
            UpdateStatus();
        };

        StyleSecondaryButton(_checkUpdatesButton, new Point(0, 8), new Size(148, 28));
        _checkUpdatesButton.Text = "Проверить обновления";
        _checkUpdatesButton.Click += async (_, _) => await CheckForUpdatesAsync();
        _headerPanel.Controls.Add(_checkUpdatesButton);

        StyleSecondaryButton(_securityButton, new Point(0, 8), new Size(92, 28));
        _securityButton.Text = "Настройки";
        _securityButton.Click += (_, _) => SetPage("settings");
        _headerPanel.Controls.Add(_securityButton);

        StyleSecondaryButton(_designButton, new Point(0, 8), new Size(92, 28));
        _designButton.Text = "Дизайн";
        _designButton.Click += (_, _) =>
        {
            _modernDesign = !_modernDesign;
            SaveUiPreferences();
            ApplyDesignLayout();
            BuildPage();
            UpdatePreferenceControlText();
            ApplyThemeToTree(this);
            ApplyUiLanguage(this);
            UpdateStatus();
        };
        _headerToolTip.SetToolTip(_designButton, TranslateUiText(_modernDesign ? "Switch to classic design" : "Switch to modern design"));
        _headerPanel.Controls.Add(_designButton);

        StyleSecondaryButton(_themeButton, new Point(0, 8), new Size(112, 28));
        _themeButton.Click += (_, _) =>
        {
            _darkTheme = !_darkTheme;
            Program.ApplyColorMode(_darkTheme);
            SaveUiPreferences();
            BuildPage();
            RefreshThemeAwareHandles(this);
            UpdatePreferenceControlText();
            ApplyThemeToTree(this);
            ApplyUiLanguage(this);
            UpdateStatus();
        };
        _headerPanel.Controls.Add(_themeButton);
        UpdatePreferenceControlText();

        StyleSecondaryButton(_lockNowButton, new Point(0, 8), new Size(124, 28));
        _lockNowButton.Text = "Заблокировать";
        _lockNowButton.Visible = IsPinLockConfigured();
        _headerPanel.Controls.Add(_lockNowButton);

        _statusLabel.Size = UiSize(292, 34);
        _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        _statusLabel.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
        _statusLabel.BackColor = Color.FromArgb(226, 228, 219);
        _statusLabel.ForeColor = Muted;
        _headerPanel.Controls.Add(_statusLabel);
        _headerPanel.SizeChanged += (_, _) => LayoutHeaderStatus();

        _pagePanel.Dock = DockStyle.Fill;
        _pagePanel.Margin = Padding.Empty;
        _pagePanel.AutoScroll = true;
        _pagePanel.BackColor = Canvas;
        _pagePanel.Padding = UiPadding(34, 8, 34, 28);
        _pagePanel.ClientSizeChanged += (_, _) => RefreshPageLayoutForWidth();
        ApplyDesignLayout();
        BuildLockOverlay();
        LayoutHeaderStatus();
    }

    private void ApplyDesignLayout()
    {
        if (!_shell.IsHandleCreated && _shell.Parent is null) return;
        _shell.SuspendLayout();
        _railLayout.SuspendLayout();
        _navigationRail.SuspendLayout();
        bool modern = _modernDesign;

        _shell.Controls.Clear();
        _shell.ColumnStyles.Clear();
        _shell.RowStyles.Clear();
        _shell.ColumnCount = modern ? 1 : 2;
        _shell.RowCount = modern ? 3 : 2;
        if (modern)
        {
            _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _shell.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleUi(132)));
            _shell.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleUi(58)));
            _shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _railLayout.Controls.Clear();
            if (_brandPanel.Parent == _headerPanel) _headerPanel.Controls.Remove(_brandPanel);
            _headerPanel.Controls.Add(_brandPanel);
            _brandPanel.Dock = DockStyle.None;
            _brandPanel.Location = UiPoint(12, 2);
            _brandPanel.Size = UiSize(220, 58);
            _brandPanel.BackColor = Canvas;
            _brandIcon.Location = UiPoint(7, 10);
            _brandIcon.Size = UiSize(40, 40);
            _brandName.Location = UiPoint(54, 4);
            _brandName.Font = new Font("Segoe UI Semibold", 22, FontStyle.Bold);
            _brandSubtitle.Location = UiPoint(55, 39);
            _brandSubtitle.ForeColor = Muted;

            _railLayout.ColumnCount = 2;
            _railLayout.RowCount = 1;
            _railLayout.ColumnStyles.Clear();
            _railLayout.RowStyles.Clear();
            _railLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _railLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleUi(224)));
            _railLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _navigationList.FlowDirection = FlowDirection.LeftToRight;
            _navigationList.WrapContents = true;
            _navigationList.Padding = UiPadding(10, 6, 10, 6);
            _railLayout.Controls.Add(_navigationList, 0, 0);
            _railLayout.Controls.Add(_railFooter, 1, 0);
            _railFooter.Dock = DockStyle.Fill;
            foreach (Button button in _navigation)
            {
                button.TextAlign = ContentAlignment.MiddleCenter;
                button.Padding = Padding.Empty;
                button.Margin = UiPadding(4, 4, 4, 4);
            }
            _shell.Controls.Add(_headerPanel, 0, 0);
            _shell.Controls.Add(_navigationRail, 0, 1);
            _shell.SetRowSpan(_navigationRail, 1);
            _shell.Controls.Add(_pagePanel, 0, 2);
            _pagePanel.Padding = UiPadding(34, 18, 34, 28);
        }
        else
        {
            _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleUi(224)));
            _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _shell.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleUi(100)));
            _shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            if (_brandPanel.Parent == _headerPanel) _headerPanel.Controls.Remove(_brandPanel);
            _railLayout.ColumnCount = 1;
            _railLayout.RowCount = 3;
            _railLayout.ColumnStyles.Clear();
            _railLayout.RowStyles.Clear();
            _railLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _railLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleUi(112)));
            _railLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _railLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleUi(62)));
            _railLayout.Controls.Clear();
            _railLayout.Controls.Add(_brandPanel, 0, 0);
            _railLayout.Controls.Add(_navigationList, 0, 1);
            _railLayout.Controls.Add(_railFooter, 0, 2);
            _brandPanel.Dock = DockStyle.Fill;
            _brandPanel.Location = Point.Empty;
            _brandPanel.BackColor = Rail;
            _brandIcon.Location = UiPoint(20, 23);
            _brandIcon.Size = UiSize(38, 38);
            _brandName.Location = UiPoint(64, 22);
            _brandName.Font = new Font("Segoe UI Semibold", 22, FontStyle.Bold);
            _brandSubtitle.Location = UiPoint(27, 70);
            _brandSubtitle.ForeColor = Color.FromArgb(144, 161, 145);
            _navigationList.FlowDirection = FlowDirection.TopDown;
            _navigationList.WrapContents = false;
            _navigationList.Padding = UiPadding(18, 8, 18, 8);
            _railFooter.Dock = DockStyle.Fill;
            foreach (Button button in _navigation)
            {
                button.TextAlign = ContentAlignment.MiddleLeft;
                button.Padding = new Padding(14, 0, 0, 0);
                button.Margin = Padding.Empty;
                button.Size = UiSize(188, 38);
            }
            _shell.Controls.Add(_navigationRail, 0, 0);
            _shell.SetRowSpan(_navigationRail, 2);
            _shell.Controls.Add(_headerPanel, 1, 0);
            _shell.Controls.Add(_pagePanel, 1, 1);
            _pagePanel.Padding = UiPadding(34, 8, 34, 28);
        }
        _navigationRail.BackColor = Rail;
        _railLayout.BackColor = Rail;
        _brandPanel.BringToFront();
        _shell.ResumeLayout(true);
        _navigationRail.ResumeLayout(true);
        _railLayout.ResumeLayout(true);
        _lastPageViewportWidth = 0;
        LayoutHeaderStatus();
        LayoutModernNavigation();
        QueueModernNavigationLayout();
    }

    private void QueueModernNavigationLayout()
    {
        if (!_modernDesign || !IsHandleCreated || IsDisposed || _modernNavigationLayoutQueued) return;
        _modernNavigationLayoutQueued = true;
        BeginInvoke((Action)(() =>
        {
            _modernNavigationLayoutQueued = false;
            if (!_modernDesign || IsDisposed || !_shell.IsHandleCreated || _navigationList.ClientSize.Width <= 0) return;
            _railLayout.PerformLayout();
            _navigationRail.PerformLayout();
            _shell.PerformLayout();
            LayoutModernNavigation();
        }));
    }

    private void LayoutModernNavigation()
    {
        if (!_modernDesign || _navigationList.ClientSize.Width <= 0 || _shell.RowStyles.Count < 2 ||
            _navigation.Any(button => button is null)) return;
        int available = _navigationList.ClientSize.Width - _navigationList.Padding.Horizontal;
        int itemMargin = ScaleUi(8);
        int minimumButtonWidth = ScaleUi(116);
        int columns = Math.Clamp(available / Math.Max(1, minimumButtonWidth + itemMargin), 1, _navigation.Length);
        int buttonWidth = Math.Max(minimumButtonWidth, available / columns - itemMargin);
        foreach (Button button in _navigation)
            button.Size = new Size(buttonWidth, ScaleUi(36));
        int rows = (int)Math.Ceiling(_navigation.Length / (double)columns);
        int desiredHeight = ScaleUi(12 + rows * 44);
        RowStyle navigationRow = _shell.RowStyles[1];
        if (navigationRow.SizeType != SizeType.Absolute || Math.Abs(navigationRow.Height - desiredHeight) > 0.5f)
        {
            navigationRow.SizeType = SizeType.Absolute;
            navigationRow.Height = desiredHeight;
            _shell.PerformLayout();
        }
    }

    private void BuildLockOverlay()
    {
        _lockOverlay.Dock = DockStyle.Fill;
        _lockOverlay.Visible = false;
        _lockOverlay.BackColor = Canvas;
        Controls.Add(_lockOverlay);

        var card = new Panel
        {
            Size = UiSize(560, 430),
            BackColor = Surface,
            BorderStyle = BorderStyle.FixedSingle
        };
        _lockOverlay.Controls.Add(card);
        void CenterCard()
        {
            card.Location = new Point(Math.Max(ScaleUi(18), (_lockOverlay.ClientSize.Width - card.Width) / 2),
                Math.Max(ScaleUi(18), (_lockOverlay.ClientSize.Height - card.Height) / 2));
        }
        _lockOverlay.Resize += (_, _) => CenterCard();
        CenterCard();

        var title = new Label
        {
            Text = "GeoCoin-Start заблокирован",
            Location = UiPoint(28, 24),
            Size = UiSize(500, 38),
            Font = new Font("Segoe UI Semibold", 20, FontStyle.Bold),
            ForeColor = Ink
        };
        card.Controls.Add(title);
        AddLabel(card, "Текущая информация", 28, 82, 480, 22, Muted, 9, bold: true);
        _lockBalance.Location = UiPoint(28, 114);
        _lockBalance.Size = UiSize(480, 28);
        _lockBalance.ForeColor = Ink;
        card.Controls.Add(_lockBalance);
        _lockBlocks.Location = UiPoint(28, 148);
        _lockBlocks.Size = UiSize(480, 28);
        _lockBlocks.ForeColor = Ink;
        card.Controls.Add(_lockBlocks);
        _lockHashrate.Location = UiPoint(28, 182);
        _lockHashrate.Size = UiSize(480, 28);
        _lockHashrate.ForeColor = Ink;
        card.Controls.Add(_lockHashrate);
        _lockNodeStatus.Location = UiPoint(28, 216);
        _lockNodeStatus.Size = UiSize(480, 46);
        _lockNodeStatus.ForeColor = Muted;
        card.Controls.Add(_lockNodeStatus);
        AddLabel(card, "Введите PIN-код приложения", 28, 286, 480, 22, Ink, 10, bold: true);
        StyleTextBox(_lockPinInput, new Point(28, 314), new Size(150, 38));
        _lockPinInput.MaxLength = 6;
        _lockPinInput.UseSystemPasswordChar = true;
        _lockPinInput.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
        };
        _lockPinInput.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            UnlockApplication();
        };
        card.Controls.Add(_lockPinInput);
        _lockHelloButton.Text = "Windows Hello";
        StyleSecondaryButton(_lockHelloButton, new Point(190, 312), new Size(156, 42));
        _lockHelloButton.Visible = _windowsHelloEnabled;
        card.Controls.Add(_lockHelloButton);
        _lockUnlockButton.Text = "Разблокировать";
        StylePrimaryButton(_lockUnlockButton, new Point(360, 312), new Size(170, 42));
        _lockUnlockButton.Click += (_, _) => UnlockApplication();
        card.Controls.Add(_lockUnlockButton);
        _lockMessage.Location = UiPoint(28, 366);
        _lockMessage.Size = UiSize(480, 38);
        _lockMessage.ForeColor = Muted;
        card.Controls.Add(_lockMessage);
    }

    private void LayoutHeaderStatus()
    {
        if (_statusLabel.IsDisposed || _headerPanel.IsDisposed) return;
        if (_modernDesign)
        {
            LayoutWorkspaceHeader();
            return;
        }
        int width = _headerPanel.ClientSize.Width;
        int standardMargin = ScaleUi(34);
        int standardGap = ScaleUi(8);
        int standardControlsWidth = ScaleUi(112 + 92 + 92 + 148 + 112 + (_lockNowButton.Visible ? 124 : 0));
        bool compactToolbar = standardMargin * 2 + standardControlsWidth + standardGap * (_lockNowButton.Visible ? 5 : 4) > width;
        int margin = ScaleUi(compactToolbar ? 18 : 34);
        int controlGap = ScaleUi(compactToolbar ? 4 : 8);
        int themeWidth = ScaleUi(compactToolbar ? 100 : 112);
        int designWidth = ScaleUi(compactToolbar ? 82 : 92);
        int securityWidth = ScaleUi(compactToolbar ? 82 : 92);
        int updateWidth = ScaleUi(compactToolbar ? 124 : 148);
        int languageWidth = ScaleUi(compactToolbar ? 102 : 112);
        int lockWidth = ScaleUi(124);
        int statusWidth = Math.Min(_statusLabel.Width > 0 ? _statusLabel.Width : ScaleUi(292), Math.Max(ScaleUi(160), width - margin * 2));
        _languageCombo.Size = new Size(languageWidth, ScaleUi(28));
        _checkUpdatesButton.Size = new Size(updateWidth, ScaleUi(28));
        _securityButton.Size = new Size(securityWidth, ScaleUi(28));
        _designButton.Size = new Size(designWidth, ScaleUi(28));
        _themeButton.Size = new Size(themeWidth, ScaleUi(28));
        int right = width - margin;
        int topRowY = ScaleUi(8);
        _themeButton.Location = new Point(Math.Max(margin, right - themeWidth), topRowY);
        _designButton.Location = new Point(Math.Max(margin, _themeButton.Left - controlGap - designWidth), topRowY);
        int lockTopRowWidth = _lockNowButton.Visible && !compactToolbar ? lockWidth + controlGap : 0;
        int lockLeft = compactToolbar
            ? right - statusWidth - controlGap - lockWidth
            : _designButton.Left - controlGap - lockWidth;
        _lockNowButton.Location = new Point(Math.Max(margin, lockLeft), compactToolbar ? ScaleUi(43) : topRowY);
        _securityButton.Location = new Point(Math.Max(margin, _designButton.Left - controlGap - securityWidth - lockTopRowWidth), topRowY);
        _checkUpdatesButton.Location = new Point(Math.Max(margin, _securityButton.Left - controlGap - updateWidth), topRowY);
        _languageCombo.Location = new Point(Math.Max(margin, _checkUpdatesButton.Left - controlGap - languageWidth), topRowY);
        _statusLabel.Size = new Size(statusWidth, ScaleUi(34));
        _statusLabel.Location = new Point(Math.Max(margin, right - statusWidth), ScaleUi(43));

        int titleX = ScaleUi(34);
        int titleGap = ScaleUi(12);
        int titleWidthInTopRow = _languageCombo.Left - titleX - titleGap;
        bool compact = compactToolbar || MeasurePageTitle(21f) > titleWidthInTopRow;
        _pageSubtitle.Visible = !compact;

        _pageTitle.AutoEllipsis = compact;
        _pageTitle.TextAlign = ContentAlignment.MiddleLeft;
        if (!compact)
        {
            _pageTitle.AutoSize = true;
            _pageTitle.Location = UiPoint(34, 20);
            _pageSubtitle.Location = UiPoint(37, 61);
            _pageSubtitle.Size = new Size(Math.Max(ScaleUi(40), _statusLabel.Left - ScaleUi(49)), ScaleUi(24));
            SetPageTitleFont(21f);
            return;
        }

        int titleRightLimit = compactToolbar && _lockNowButton.Visible ? _lockNowButton.Left - titleGap : _statusLabel.Left;
        int titleWidth = Math.Max(ScaleUi(40), titleRightLimit - titleX - titleGap);
        float titleFontSize = 21f;
        while (titleFontSize > 15f && MeasurePageTitle(titleFontSize) > titleWidth)
            titleFontSize -= 0.5f;

        _pageTitle.AutoSize = false;
        _pageTitle.Location = new Point(titleX, ScaleUi(43));
        _pageTitle.Size = new Size(titleWidth, ScaleUi(34));
        SetPageTitleFont(titleFontSize);
    }

    private void LayoutWorkspaceHeader()
    {
        int width = _headerPanel.ClientSize.Width;
        if (width <= 0) return;
        int margin = ScaleUi(18);
        int gap = ScaleUi(6);
        int brandArea = ScaleUi(244);
        int themeWidth = ScaleUi(100);
        int designWidth = ScaleUi(82);
        int securityWidth = ScaleUi(82);
        int updateWidth = ScaleUi(124);
        int languageWidth = ScaleUi(100);
        int lockWidth = ScaleUi(108);
        int controlCount = _lockNowButton.Visible ? 6 : 5;
        int controlsWidth = themeWidth + designWidth + securityWidth + updateWidth + languageWidth +
            (_lockNowButton.Visible ? lockWidth : 0) + gap * (controlCount - 1);
        if (brandArea + controlsWidth + margin * 2 > width)
        {
            themeWidth = ScaleUi(92);
            designWidth = ScaleUi(76);
            securityWidth = ScaleUi(76);
            updateWidth = ScaleUi(112);
            languageWidth = ScaleUi(92);
            lockWidth = ScaleUi(96);
            gap = ScaleUi(4);
        }

        _brandPanel.Location = UiPoint(12, 2);
        _brandPanel.Size = UiSize(220, 58);
        int right = width - margin;
        int top = ScaleUi(8);
        _themeButton.Size = new Size(themeWidth, ScaleUi(28));
        _designButton.Size = new Size(designWidth, ScaleUi(28));
        _securityButton.Size = new Size(securityWidth, ScaleUi(28));
        _checkUpdatesButton.Size = new Size(updateWidth, ScaleUi(28));
        _languageCombo.Size = new Size(languageWidth, ScaleUi(28));
        _lockNowButton.Size = new Size(lockWidth, ScaleUi(28));
        _themeButton.Location = new Point(right - themeWidth, top);
        _designButton.Location = new Point(_themeButton.Left - gap - designWidth, top);
        int nextLeft = _designButton.Left - gap;
        if (_lockNowButton.Visible)
        {
            _lockNowButton.Location = new Point(nextLeft - lockWidth, top);
            nextLeft = _lockNowButton.Left - gap;
        }
        _securityButton.Location = new Point(nextLeft - securityWidth, top);
        _checkUpdatesButton.Location = new Point(_securityButton.Left - gap - updateWidth, top);
        _languageCombo.Location = new Point(_checkUpdatesButton.Left - gap - languageWidth, top);

        int statusWidth = Math.Min(ScaleUi(292), Math.Max(ScaleUi(170), width - margin * 2));
        _statusLabel.Size = new Size(statusWidth, ScaleUi(34));
        _statusLabel.Location = new Point(right - statusWidth, ScaleUi(47));

        int titleX = ScaleUi(28);
        int titleWidth = Math.Max(ScaleUi(120), _statusLabel.Left - titleX - ScaleUi(14));
        _pageTitle.AutoSize = false;
        _pageTitle.AutoEllipsis = true;
        _pageTitle.TextAlign = ContentAlignment.MiddleLeft;
        _pageTitle.Location = new Point(titleX, ScaleUi(69));
        _pageTitle.Size = new Size(titleWidth, ScaleUi(34));
        SetPageTitleFont(21f);
        _pageSubtitle.Visible = true;
        _pageSubtitle.Location = UiPoint(32, 103);
        _pageSubtitle.Size = new Size(Math.Max(ScaleUi(120), _statusLabel.Left - ScaleUi(42)), ScaleUi(22));
    }

    private int MeasurePageTitle(float fontSize)
    {
        using var font = new Font("Segoe UI Semibold", fontSize, FontStyle.Bold);
        return TextRenderer.MeasureText(_pageTitle.Text, font, Size.Empty, TextFormatFlags.NoPadding).Width;
    }

    private void SetPageTitleFont(float fontSize)
    {
        if (Math.Abs(_pageTitle.Font.SizeInPoints - fontSize) < 0.1f) return;
        Font previous = _pageTitle.Font;
        _pageTitle.Font = new Font("Segoe UI Semibold", fontSize, FontStyle.Bold);
        previous.Dispose();
    }

    private void RefreshPageLayoutForWidth()
    {
        int width = _pagePanel.ClientSize.Width;
        if (!IsHandleCreated || IsDisposed || width <= 0 || width == _lastPageViewportWidth) return;
        _lastPageViewportWidth = width;
        BeginInvoke((Action)BuildPage);
    }

    private int PageCardWidth(int preferredWidth)
    {
        int availablePixels = _pagePanel.ClientSize.Width - _pagePanel.Padding.Horizontal - ScaleUi(44);
        if (availablePixels <= 0) return preferredWidth;
        int availableLogical = (int)Math.Floor(availablePixels * 96d / Math.Max(96, DeviceDpi));
        return Math.Max(520, Math.Min(preferredWidth, availableLogical));
    }

    private int ScaleUi(int value) => (int)Math.Round(value * DeviceDpi / 96d);
    private Point UiPoint(int x, int y) => new(ScaleUi(x), ScaleUi(y));
    private Size UiSize(int width, int height) => new(ScaleUi(width), ScaleUi(height));
    private Padding UiPadding(int left, int top, int right, int bottom)
        => new(ScaleUi(left), ScaleUi(top), ScaleUi(right), ScaleUi(bottom));

    private void ScrollPageWithMouseWheel(int delta)
    {
        if (delta == 0 || !_pagePanel.IsHandleCreated || _pagePanel.IsDisposed || !_pagePanel.VerticalScroll.Visible) return;
        int currentY = Math.Max(0, -_pagePanel.AutoScrollPosition.Y);
        int maxY = Math.Max(0, _pagePanel.DisplayRectangle.Height - _pagePanel.ClientSize.Height);
        int notches = Math.Max(1, Math.Abs(delta) / 120);
        int targetY = Math.Clamp(currentY - Math.Sign(delta) * ScaleUi(48) * notches, 0, maxY);
        _pagePanel.AutoScrollPosition = new Point(Math.Max(0, -_pagePanel.AutoScrollPosition.X), targetY);
    }

    private void FitWindowToScreen()
    {
        Rectangle workArea = Screen.FromControl(this).WorkingArea;
        int width = Math.Max(720, Math.Min(Width, workArea.Width - 24));
        int height = Math.Max(540, Math.Min(Height, workArea.Height - 24));
        if (width >= Width && height >= Height) return;

        MinimumSize = new Size(Math.Min(MinimumSize.Width, width), Math.Min(MinimumSize.Height, height));
        Size = new Size(width, height);
        CenterToScreen();
    }

    private void SetPage(string page)
    {
        if (!string.Equals(_currentPage, page, StringComparison.Ordinal))
            _pagePanel.AutoScrollPosition = Point.Empty;
        if (_currentPage == "wallet" && page != "wallet") _privateKeyInput.Clear();
        _currentPage = page;
        string[] titles = page switch
        {
            "mining" => new[] { "Майнинг", "Майнинг", "Майнинг", "Майнинг" },
            "subminer" => new[] { "Подмайнер", "Подмайнер", "Подмайнер", "Подмайнер" },
            "node" => new[] { "Нода и журнал", "Нода и журнал", "Нода и журнал", "Нода и журнал" },
            "send" => new[] { "Отправить монеты", "Отправить монеты", "Отправить монеты", "Отправить монеты" },
            "receive" => new[] { "Получить монеты", "Получить монеты", "Получить монеты", "Получить монеты" },
            "transactions" => new[] { "История транзакций", "История транзакций", "История транзакций", "История транзакций" },
            "wallet" => new[] { "Управление кошельком", "Управление кошельком", "Управление кошельком", "Управление кошельком" },
            "guide" => new[] { "Помощник первого запуска", "Помощник первого запуска", "Помощник первого запуска", "Помощник первого запуска" },
            "settings" => new[] { "Настройки", "Настройки", "Настройки", "Настройки" },
            _ => new[] { "Обзор", "Обзор", "Обзор", "Обзор" }
        };
        _pageTitle.Text = titles[0];
        string[] pages = { "overview", "mining", "subminer", "node", "send", "receive", "transactions", "wallet", "guide", "settings" };
        for (int i = 0; i < _navigation.Length; i++)
        {
            bool selected = pages[i] == page;
            _navigation[i].BackColor = selected
                ? (_darkTheme ? Color.FromArgb(48, 61, 52) : Color.FromArgb(44, 59, 48))
                : Rail;
            _navigation[i].ForeColor = selected ? Color.FromArgb(241, 218, 173) : Color.FromArgb(206, 215, 205);
            if (selected && _navigation[i].Parent is ScrollableControl navigationScroll)
                navigationScroll.ScrollControlIntoView(_navigation[i]);
        }
        BuildPage();
        if (page == "transactions" && IsNodeRunning() && File.Exists(AppPaths.WalletPath))
            _ = RefreshTransactionsAsync();
        if (page == "wallet" && IsNodeRunning() && File.Exists(AppPaths.WalletPath))
            _ = RefreshWalletAddressesAsync();
        if (page == "receive" && IsNodeRunning() && File.Exists(AppPaths.WalletPath))
            _ = RefreshReceiveAddressesAsync();
        if ((page == "mining" || page == "subminer") && !_gpuDevicesScanned && !_checkingGpuDevices && File.Exists(AppPaths.GpuMinerPath))
            _ = RefreshGpuDevicesAsync();
        if (page == "node") UpdateNodePage();
        ApplyUiLanguage(this);
    }

    private void BuildPage()
    {
        int savedScrollX = Math.Max(0, -_pagePanel.AutoScrollPosition.X);
        int savedScrollY = Math.Max(0, -_pagePanel.AutoScrollPosition.Y);
        _pagePanel.SuspendLayout();
        _pagePanel.Controls.Clear();
        _transactionGrid = null;
        switch (_currentPage)
        {
            case "mining": BuildMiningPage(); break;
            case "subminer": BuildSubminerPage(); break;
            case "node": BuildNodePage(); break;
            case "send": BuildSendPage(); break;
            case "receive": BuildReceivePage(); break;
            case "transactions": BuildTransactionsPage(); break;
            case "wallet": BuildWalletPage(); break;
            case "guide": BuildFirstRunGuidePage(); break;
            case "settings": BuildSettingsPage(); break;
            default: BuildOverviewPage(); break;
        }
        _pagePanel.ResumeLayout();
        _pagePanel.PerformLayout();
        CenterWorkspacePageControls();
        _pagePanel.AutoScrollPosition = new Point(savedScrollX, savedScrollY);
        ApplyThemeToTree(this);
        ApplyUiLanguage(this);
        UpdateStatus();
    }

    private void CenterWorkspacePageControls()
    {
        if (!_modernDesign || _pagePanel.Controls.Count == 0) return;
        int left = _pagePanel.Controls.Cast<Control>().Min(control => control.Left);
        int right = _pagePanel.Controls.Cast<Control>().Max(control => control.Right);
        int contentWidth = right - left;
        int availableWidth = _pagePanel.ClientSize.Width - _pagePanel.Padding.Horizontal;
        if (contentWidth <= 0 || contentWidth >= availableWidth - ScaleUi(24)) return;
        int shift = (availableWidth - contentWidth) / 2 - left;
        if (shift == 0) return;
        foreach (Control control in _pagePanel.Controls)
            control.Left += shift;
    }

    private int GetFirstRunStep()
    {
        if (!IsNodeRunning()) return 1;
        if (!File.Exists(AppPaths.WalletPath)) return 2;
        return _firstRunBackupConfirmed ? 4 : 3;
    }

    private void BuildFirstRunGuidePage()
    {
        int cardWidth = PageCardWidth(740);
        bool completed = _firstRunGuideCompleted;
        AddParagraph(completed
                ? "Обучение пройдено. Ваш прогресс сохранён, пользоваться приложением можно через меню."
                : "Пошаговый помощник проведёт через подключение, кошелёк и первый запуск. Вы можете перейти к любому разделу в меню.",
            22, 12, cardWidth, 54);
        Panel card = AddCard(22, 82, cardWidth, 360);

        int step = GetFirstRunStep();
        string title = completed ? "Обучение пройдено" : step switch
        {
            1 => "Шаг 1 из 4 · Подключите ноду",
            2 => "Шаг 2 из 4 · Создайте или добавьте кошелёк",
            3 => "Шаг 3 из 4 · Сохраните резервную копию",
            _ => "Шаг 4 из 4 · Завершите обучение"
        };
        string description = completed
            ? "Обучение завершено. Статус сохранён на этом компьютере; при следующем открытии помощника вы увидите этот результат."
            : step switch
        {
            1 => "Нода подключает GeoCoin-Start к сети и синхронизирует блоки. Нажмите «Запустить ноду» и дождитесь соединения.",
            2 => "Создайте новый кошелёк или импортируйте существующий файл .dat. Пароль кошелька потребуется при подключении.",
            3 => "Экспортируйте файл кошелька .dat и храните его отдельно. Резервная копия поможет восстановить доступ при поломке компьютера.",
            _ => "Основные шаги выполнены. Майнинг и создание адреса остаются на ваше усмотрение. Нажмите кнопку, чтобы сохранить прохождение помощника."
        };
        AddLabel(card, title, 26, 24, cardWidth - 80, 34, Ink, 17, bold: true);
        AddLabel(card, description, 26, 75, cardWidth - 80, 58, Muted, 10.5f);

        string[] steps = { "Нода", "Кошелёк", "Резервная копия", "Перевод или майнинг" };
        int tileGap = 8;
        int tileWidth = (cardWidth - 52 - tileGap * 3) / steps.Length;
        for (int index = 0; index < steps.Length; index++)
        {
            int progress = completed || index < step - 1 ? 100 : 0;
            bool currentStep = !completed && index == step - 1;
            var tile = new GuideProgressTile
            {
                Text = steps[index],
                StepNumber = index + 1,
                ProgressPercent = progress,
                IsCurrent = currentStep,
                Location = UiPoint(26 + index * (tileWidth + tileGap), 156),
                Size = UiSize(tileWidth, 70),
                BackColor = Surface,
                ForeColor = Ink,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                BorderColor = currentStep ? Accent : Divider,
                TrackColor = Divider,
                ProgressColor = progress >= 100 ? Success : currentStep ? Accent : Divider,
                AccessibleName = $"{TranslateUiText(steps[index])}, {progress}%"
            };
            card.Controls.Add(tile);
        }

        _guideActionButton.Visible = true;
        _guideActionButton.Text = completed ? "Открыть обзор" : step switch
        {
            1 => "Запустить ноду",
            2 => "Создать кошелёк",
            3 => "Открыть резервные копии",
            _ => "Завершить помощник"
        };
        int guideButtonWidth = completed || step == 4 ? Math.Min(300, cardWidth - 76) : Math.Min(252, (cardWidth - 76) / 2);
        StylePrimaryButton(_guideActionButton, new Point(26, 244), new Size(guideButtonWidth, 44));
        _guideActionButton.Enabled = completed || step != 1 || !_startingNode;
        card.Controls.Add(_guideActionButton);

        _guideSecondaryButton.Visible = !completed && step < 4;
        _guideSecondaryButton.Text = step switch
        {
            1 => "Открыть страницу ноды",
            2 => "Импортировать кошелёк",
            3 => "Экспортировать копию сейчас",
            _ => "Открыть получение монет"
        };
        if (_guideSecondaryButton.Visible)
        {
            StyleSecondaryButton(_guideSecondaryButton, new Point(40 + guideButtonWidth, 244), new Size(guideButtonWidth, 44));
            card.Controls.Add(_guideSecondaryButton);
        }

        _guideDoneButton.Visible = !completed && step < 4;
        _guideDoneButton.Text = step == 3 ? "Я сохранил резервную копию" : "Пока пропустить помощник";
        if (_guideDoneButton.Visible)
        {
            StyleSecondaryButton(_guideDoneButton, new Point(26, 300), new Size(300, 40));
            card.Controls.Add(_guideDoneButton);
        }

        if (!completed)
            AddParagraph("PIN-код блокирует доступ к окну GeoCoin-Start, но не заменяет пароль кошелька и не шифрует файл кошелька. Настроить PIN и уведомления можно в разделе «Настройки».",
                22, 466, cardWidth, 60);
    }

    private void BuildSettingsPage()
    {
        int cardWidth = PageCardWidth(740);
        int contentWidth = cardWidth - 52;
        AddParagraph("Управляйте защитой приложения, системными уведомлениями, автозапуском и первым запуском.", 22, 12, cardWidth, 42);

        Panel securityCard = AddCard(22, 66, cardWidth, 500);
        AddLabel(securityCard, "БЕЗОПАСНОСТЬ", 24, 18, contentWidth, 22, Muted, 9, bold: true);
        _pinSettingsStatus.Text = IsPinLockConfigured()
            ? "PIN-защита включена для текущей учётной записи Windows."
            : "PIN-защита выключена.";
        _pinSettingsStatus.Location = UiPoint(24, 47);
        _pinSettingsStatus.Size = UiSize(contentWidth, 26);
        _pinSettingsStatus.ForeColor = IsPinLockConfigured() ? Success : Muted;
        securityCard.Controls.Add(_pinSettingsStatus);

        AddLabel(securityCard, "Длина PIN", 24, 84, 190, 26, Ink, 10, bold: true);
        _pinLengthCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        if (_pinLengthCombo.Items.Count == 0) _pinLengthCombo.Items.AddRange(new object[] { "4", "6" });
        if (_pinLengthCombo.SelectedIndex < 0) _pinLengthCombo.SelectedIndex = 0;
        _pinLengthCombo.Location = UiPoint(224, 80);
        _pinLengthCombo.Size = UiSize(88, 32);
        securityCard.Controls.Add(_pinLengthCombo);

        AddLabel(securityCard, "Текущий PIN", 24, 128, 190, 26, Ink, 10, bold: true);
        StyleTextBox(_currentPinInput, new Point(224, 124), new Size(180, 32));
        _currentPinInput.Visible = IsPinLockConfigured();
        securityCard.Controls.Add(_currentPinInput);
        AddLabel(securityCard, "Новый PIN", 24, 172, 190, 26, Ink, 10, bold: true);
        StyleTextBox(_newPinInput, new Point(224, 168), new Size(180, 32));
        securityCard.Controls.Add(_newPinInput);
        AddLabel(securityCard, "Повторите новый PIN", 24, 216, 190, 26, Ink, 10, bold: true);
        StyleTextBox(_confirmPinInput, new Point(224, 212), new Size(180, 32));
        securityCard.Controls.Add(_confirmPinInput);

        _saveSecurityButton.Text = IsPinLockConfigured() ? "Сохранить PIN и настройки" : "Настроить PIN-защиту";
        StylePrimaryButton(_saveSecurityButton, new Point(24, 264), new Size(270, 40));
        securityCard.Controls.Add(_saveSecurityButton);
        _disablePinButton.Text = "Отключить PIN";
        StyleSecondaryButton(_disablePinButton, new Point(24, 314), new Size(160, 40));
        _disablePinButton.Visible = IsPinLockConfigured();
        securityCard.Controls.Add(_disablePinButton);
        _buildingSettingsPage = true;
        _autoLockCheck.Text = "Автоматически блокировать";
        _autoLockCheck.Checked = _autoLockEnabled;
        _autoLockCheck.AutoSize = false;
        _autoLockCheck.Margin = Padding.Empty;
        _autoLockCheck.Size = UiSize(238, 30);
        _autoLockCheck.Enabled = true;
        _autoLockMinutesInput.Value = Math.Clamp(_autoLockMinutes, 1, 120);
        _autoLockMinutesInput.Enabled = _autoLockEnabled;
        _autoLockMinutesInput.Size = UiSize(72, 30);
        _autoLockMinutesInput.Margin = new Padding(ScaleUi(8), 0, ScaleUi(8), 0);
        _autoLockUnitsLabel.Text = "минут бездействия";
        _autoLockUnitsLabel.Enabled = _autoLockEnabled;
        _autoLockUnitsLabel.AutoSize = false;
        _autoLockUnitsLabel.Size = UiSize(Math.Max(100, contentWidth - 350), 30);
        _autoLockUnitsLabel.Margin = Padding.Empty;
        _autoLockUnitsLabel.TextAlign = ContentAlignment.MiddleLeft;
        _autoLockUnitsLabel.ForeColor = Ink;
        _autoLockUnitsLabel.Font = new Font("Segoe UI", 9.5f);
        _autoLockCheck.Location = UiPoint(24, 366);
        _autoLockCheck.Size = UiSize(224, 32);
        _autoLockMinutesInput.Location = UiPoint(258, 366);
        _autoLockMinutesInput.Size = UiSize(76, 32);
        _autoLockMinutesInput.TextAlign = HorizontalAlignment.Left;
        _autoLockMinutesInput.Font = new Font("Segoe UI", 9.5f);
        _autoLockMinutesInput.BackColor = InputSurface;
        _autoLockMinutesInput.ForeColor = Ink;
        _autoLockUnitsLabel.Location = UiPoint(346, 366);
        _autoLockUnitsLabel.Size = UiSize(Math.Max(100, contentWidth - 352), 32);
        securityCard.Controls.Add(_autoLockCheck);
        securityCard.Controls.Add(_autoLockMinutesInput);
        securityCard.Controls.Add(_autoLockUnitsLabel);
        _windowsHelloCheck.Text = "Разрешить разблокировку с помощью Windows Hello";
        _windowsHelloCheck.Checked = _windowsHelloEnabled;
        _windowsHelloCheck.Enabled = IsPinLockConfigured();
        _windowsHelloCheck.Location = UiPoint(24, 400);
        _windowsHelloCheck.Size = UiSize(contentWidth, 30);
        securityCard.Controls.Add(_windowsHelloCheck);
        _buildingSettingsPage = false;
        AddLabel(securityCard, "PIN-код ограничивает доступ к окну приложения. Для защиты средств используйте пароль кошелька и храните резервную копию отдельно.",
            24, 438, contentWidth, 48, Muted, 9.5f);

        Panel notificationsCard = AddCard(22, 578, cardWidth, 356);
        AddLabel(notificationsCard, "СИСТЕМНЫЕ УВЕДОМЛЕНИЯ", 24, 18, contentWidth, 22, Muted, 9, bold: true);
        _notificationsCheck.Text = "Показывать уведомления Windows";
        _notificationsCheck.Checked = _notificationsEnabled;
        _notificationsCheck.Location = UiPoint(24, 48);
        _notificationsCheck.Size = UiSize(contentWidth, 28);
        notificationsCard.Controls.Add(_notificationsCheck);
        _notifyNodeStoppedCheck.Text = "Нода остановилась или потеряла соединение";
        _notifyNodeStoppedCheck.Checked = _notifyNodeStopped;
        _notifyNodeStoppedCheck.Location = UiPoint(48, 80);
        _notifyNodeStoppedCheck.Size = UiSize(contentWidth - 24, 26);
        notificationsCard.Controls.Add(_notifyNodeStoppedCheck);
        _notifySyncCheck.Text = "Нода завершила синхронизацию";
        _notifySyncCheck.Checked = _notifySyncComplete;
        _notifySyncCheck.Location = UiPoint(48, 108);
        _notifySyncCheck.Size = UiSize(contentWidth - 24, 26);
        notificationsCard.Controls.Add(_notifySyncCheck);
        _notifyMiningCheck.Text = "Обнаружена новая награда за майнинг";
        _notifyMiningCheck.Checked = _notifyMiningRewards;
        _notifyMiningCheck.Location = UiPoint(48, 136);
        _notifyMiningCheck.Size = UiSize(contentWidth - 24, 26);
        notificationsCard.Controls.Add(_notifyMiningCheck);
        _notifyIncomingTransfersCheck.Text = "Входящий перевод";
        _notifyIncomingTransfersCheck.Checked = _notifyIncomingTransfers;
        _notifyIncomingTransfersCheck.Location = UiPoint(48, 164);
        _notifyIncomingTransfersCheck.Size = UiSize(contentWidth - 24, 26);
        notificationsCard.Controls.Add(_notifyIncomingTransfersCheck);
        _notifyOutgoingTransfersCheck.Text = "Отправленный перевод принят нодой";
        _notifyOutgoingTransfersCheck.Checked = _notifyOutgoingTransfers;
        _notifyOutgoingTransfersCheck.Location = UiPoint(48, 192);
        _notifyOutgoingTransfersCheck.Size = UiSize(contentWidth - 24, 26);
        notificationsCard.Controls.Add(_notifyOutgoingTransfersCheck);
        _notifySubminerRpcCheck.Text = "Подмайнер подключился к RPC";
        _notifySubminerRpcCheck.Checked = _notifySubminerRpc;
        _notifySubminerRpcCheck.Location = UiPoint(48, 220);
        _notifySubminerRpcCheck.Size = UiSize(contentWidth - 24, 26);
        _notifySubminerRpcCheck.Enabled = _notificationsEnabled;
        notificationsCard.Controls.Add(_notifySubminerRpcCheck);
        _notifyNodeStoppedCheck.Enabled = _notificationsEnabled;
        _notifySyncCheck.Enabled = _notificationsEnabled;
        _notifyMiningCheck.Enabled = _notificationsEnabled;
        _notifyIncomingTransfersCheck.Enabled = _notificationsEnabled;
        _notifyOutgoingTransfersCheck.Enabled = _notificationsEnabled;

        _saveNotificationsButton.Text = "Сохранить уведомления";
        StylePrimaryButton(_saveNotificationsButton, new Point(24, 258), new Size(190, 38));
        notificationsCard.Controls.Add(_saveNotificationsButton);
        _reopenGuideButton.Text = "Открыть помощник первого запуска";
        StyleSecondaryButton(_reopenGuideButton, new Point(226, 258), new Size(Math.Min(288, contentWidth - 226), 38));
        notificationsCard.Controls.Add(_reopenGuideButton);
        _testNotificationButton.Text = "Отправить тестовое уведомление";
        StyleSecondaryButton(_testNotificationButton, new Point(24, 300), new Size(250, 38));
        notificationsCard.Controls.Add(_testNotificationButton);

        Panel startupCard = AddCard(22, 950, cardWidth, 154);
        AddLabel(startupCard, "АВТОЗАПУСК", 24, 18, contentWidth, 22, Muted, 9, bold: true);
        _updatingAutoStartCheck = true;
        _autoStartCheck.Text = "Запускать GeoCoin-Start при входе в Windows";
        _autoStartCheck.Checked = _autoStartEnabled;
        _autoStartCheck.Location = UiPoint(24, 48);
        _autoStartCheck.Size = UiSize(contentWidth, 28);
        startupCard.Controls.Add(_autoStartCheck);
        _updatingAutoStartCheck = false;
        AddLabel(startupCard, "Автозапуск открывает только приложение; запуск ноды и майнинга остаётся под вашим контролем.",
            24, 82, contentWidth, 48, Muted, 9.5f);
    }

    private bool IsPinLockConfigured() => !string.IsNullOrWhiteSpace(_protectedPinVerifier);

    private void SaveSecuritySettingsFromUi()
    {
        string currentPin = _currentPinInput.Text;
        string newPin = _newPinInput.Text;
        string confirmation = _confirmPinInput.Text;
        bool changingPin = newPin.Length > 0 || confirmation.Length > 0;

        if (changingPin)
        {
            if (IsPinLockConfigured() && !VerifyPin(currentPin))
            {
                ShowLocalizedMessage("Текущий PIN-код указан неверно.", "Проверьте PIN-код", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int requiredLength = int.TryParse(_pinLengthCombo.SelectedItem?.ToString(), out int length) ? length : 4;
            if (newPin.Length != requiredLength || newPin.Any(ch => ch is < '0' or > '9'))
            {
                ShowLocalizedMessage($"Новый PIN должен содержать ровно {requiredLength} цифры.", "Неверная длина PIN", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!string.Equals(newPin, confirmation, StringComparison.Ordinal))
            {
                ShowLocalizedMessage("PIN-коды не совпадают.", "Проверьте ввод", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try { _protectedPinVerifier = CreateProtectedPinVerifier(newPin); }
            catch (Exception ex)
            {
                ShowLocalizedMessage("Не удалось сохранить PIN-код: " + ex.Message, "Ошибка защиты", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        _autoLockEnabled = _autoLockCheck.Checked;
        _autoLockMinutes = (int)_autoLockMinutesInput.Value;
        _windowsHelloEnabled = _windowsHelloCheck.Checked && IsPinLockConfigured();
        _notificationsEnabled = _notificationsCheck.Checked;
        _notifyNodeStopped = _notifyNodeStoppedCheck.Checked;
        _notifySubminerRpc = _notifySubminerRpcCheck.Checked;
        _notifySyncComplete = _notifySyncCheck.Checked;
        _notifyMiningRewards = _notifyMiningCheck.Checked;
        _notifyIncomingTransfers = _notifyIncomingTransfersCheck.Checked;
        _notifyOutgoingTransfers = _notifyOutgoingTransfersCheck.Checked;
        SaveUiPreferences();
        UpdateSystemNotificationState();
        _currentPinInput.Clear();
        _newPinInput.Clear();
        _confirmPinInput.Clear();
        ShowLocalizedMessage(IsPinLockConfigured()
                ? "Настройки безопасности сохранены. PIN блокирует окно GeoCoin-Start и не заменяет пароль кошелька."
                : "Настройки уведомлений сохранены.",
            "Настройки сохранены", MessageBoxButtons.OK, MessageBoxIcon.Information);
        BuildPage();
    }

    private void SaveNotificationSettings()
    {
        _notificationsEnabled = _notificationsCheck.Checked;
        _notifyNodeStopped = _notifyNodeStoppedCheck.Checked;
        _notifySubminerRpc = _notifySubminerRpcCheck.Checked;
        _notifySyncComplete = _notifySyncCheck.Checked;
        _notifyMiningRewards = _notifyMiningCheck.Checked;
        _notifyIncomingTransfers = _notifyIncomingTransfersCheck.Checked;
        _notifyOutgoingTransfers = _notifyOutgoingTransfersCheck.Checked;
        SaveUiPreferences();
        UpdateSystemNotificationState();
        ShowLocalizedMessage("Настройки уведомлений сохранены.", "Настройки сохранены", MessageBoxButtons.OK, MessageBoxIcon.Information);
        BuildPage();
    }

    private void SaveAutoStartPreference()
    {
        if (_updatingAutoStartCheck) return;
        bool enabled = _autoStartCheck.Checked;
        try
        {
            StartupRegistration.SetEnabled(enabled);
            _autoStartEnabled = enabled;
            SaveUiPreferences();
        }
        catch (Exception ex)
        {
            _updatingAutoStartCheck = true;
            _autoStartCheck.Checked = _autoStartEnabled;
            _updatingAutoStartCheck = false;
            ShowLocalizedMessage("Не удалось изменить настройку автозапуска: " + ex.Message,
                "Ошибка автозапуска", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void DisablePinFromSettings()
    {
        if (!IsPinLockConfigured()) return;
        if (!VerifyPin(_currentPinInput.Text))
        {
            ShowLocalizedMessage("Чтобы отключить защиту, введите текущий PIN-код.", "Проверьте PIN-код", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _protectedPinVerifier = null;
        _windowsHelloEnabled = false;
        _windowsHelloCheck.Checked = false;
        _autoLockEnabled = false;
        _autoLockCheck.Checked = false;
        _currentPinInput.Clear();
        SaveUiPreferences();
        BuildPage();
    }

    private static string CreateProtectedPinVerifier(string pin)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, 180_000, HashAlgorithmName.SHA256, 32);
        byte[] payload = new byte[salt.Length + hash.Length];
        Buffer.BlockCopy(salt, 0, payload, 0, salt.Length);
        Buffer.BlockCopy(hash, 0, payload, salt.Length, hash.Length);
        byte[] protectedPayload = ProtectedData.Protect(payload, "GeoCoin-Start-PIN-v1"u8.ToArray(), DataProtectionScope.CurrentUser);
        CryptographicOperations.ZeroMemory(hash);
        CryptographicOperations.ZeroMemory(payload);
        return Convert.ToBase64String(protectedPayload);
    }

    private bool VerifyPin(string pin)
    {
        if (!IsPinLockConfigured()) return false;
        try
        {
            byte[] protectedPayload = Convert.FromBase64String(_protectedPinVerifier!);
            byte[] payload = ProtectedData.Unprotect(protectedPayload, "GeoCoin-Start-PIN-v1"u8.ToArray(), DataProtectionScope.CurrentUser);
            if (payload.Length != 48) return false;
            byte[] salt = payload.AsSpan(0, 16).ToArray();
            byte[] expected = payload.AsSpan(16, 32).ToArray();
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(pin, salt, 180_000, HashAlgorithmName.SHA256, 32);
            bool matches = CryptographicOperations.FixedTimeEquals(expected, actual);
            CryptographicOperations.ZeroMemory(payload);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(actual);
            return matches;
        }
        catch { return false; }
    }

    private void LockApplication()
    {
        if (!IsPinLockConfigured() || _applicationLocked) return;
        _applicationLocked = true;
        _lastUiActivityUtc = DateTimeOffset.UtcNow;
        _lockPinInput.Clear();
        _lockMessage.Text = "";
        UpdateLockScreenMetrics();
        _lockOverlay.BackColor = Canvas;
        _lockOverlay.Visible = true;
        _lockOverlay.BringToFront();
        ApplyThemeToTree(_lockOverlay);
        ApplyUiLanguage(_lockOverlay);
        BeginInvoke((Action)(() => _lockPinInput.Focus()));
    }

    private void UnlockApplication()
    {
        if (!_applicationLocked) return;
        if (_pinLockoutUntil is { } until && DateTimeOffset.UtcNow < until)
        {
            int seconds = Math.Max(1, (int)Math.Ceiling((until - DateTimeOffset.UtcNow).TotalSeconds));
            _lockMessage.Text = TranslateUiText("Подождите ") + seconds + TranslateUiText(" сек. перед новой попыткой.");
            return;
        }
        if (!VerifyPin(_lockPinInput.Text))
        {
            _lockPinInput.Clear();
            _pinFailedAttempts++;
            _lockMessage.ForeColor = Error;
            if (_pinFailedAttempts >= 5)
            {
                _pinFailedAttempts = 0;
                _pinLockoutUntil = DateTimeOffset.UtcNow.AddSeconds(30);
                _lockMessage.Text = TranslateUiText("Слишком много попыток. Подождите 30 секунд.");
            }
            else _lockMessage.Text = TranslateUiText("PIN-код не подходит. Попробуйте ещё раз.");
            return;
        }
        CompleteApplicationUnlock();
    }

    private async Task UnlockWithWindowsHelloAsync()
    {
        if (!_applicationLocked || !_windowsHelloEnabled) return;
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            _lockMessage.ForeColor = Muted;
            _lockMessage.Text = TranslateUiText("Windows Hello доступен начиная с Windows 11. Используйте PIN-код приложения.");
            return;
        }
        _lockHelloButton.Enabled = false;
        try
        {
            Windows.Security.Credentials.UI.UserConsentVerificationResult result =
                await Windows.Security.Credentials.UI.UserConsentVerifierInterop.RequestVerificationForWindowAsync(
                    Handle, TranslateUiText("Подтвердите личность, чтобы разблокировать GeoCoin-Start."));
            if (result == Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified)
            {
                CompleteApplicationUnlock();
                return;
            }
            _lockMessage.ForeColor = Muted;
            _lockMessage.Text = TranslateUiText("Windows Hello не подтвердил вход. Можно попробовать ещё раз или ввести PIN.");
        }
        catch (Exception ex)
        {
            _lockMessage.ForeColor = Error;
            _lockMessage.Text = TranslateUiText("Windows Hello недоступен. Используйте PIN-код приложения.") + " " + ex.Message;
        }
        finally { _lockHelloButton.Enabled = true; }
    }

    private void CompleteApplicationUnlock()
    {
        _pinFailedAttempts = 0;
        _pinLockoutUntil = null;
        _applicationLocked = false;
        _lastUiActivityUtc = DateTimeOffset.UtcNow;
        _lockOverlay.Visible = false;
        _lockOverlay.SendToBack();
        _shell.Visible = true;
        _shell.BringToFront();
        _lockPinInput.Clear();
        _shell.PerformLayout();
        _shell.Invalidate(true);
        Invalidate(true);
        Update();
        Activate();
    }

    private void UpdateLockScreenMetrics()
    {
        if (_lockOverlay.IsDisposed) return;
        int lifetime;
        int today;
        int session;
        DateTime todayDate = DateTime.Now.Date;
        lock (_minedBlocksLock)
        {
            lifetime = _lifetimeMinedBlockIds.Count;
            today = _minedBlockRecords.Where(record => record.Timestamp > 0 &&
                    DateTimeOffset.FromUnixTimeSeconds(record.Timestamp).ToLocalTime().Date == todayDate)
                .Select(record => record.BlockId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            session = _miningSessionBlockIds.Count;
        }
        _lockBalance.Text = TranslateUiText("Баланс: ") +
            (IsNodeRunning() ? FormatGcAmount(_balance) : "— GC");
        _lockBlocks.Text = TranslateUiText("Добыто блоков: ") + lifetime.ToString("N0", CultureInfo.CurrentCulture) +
            TranslateUiText(" · сегодня: ") + today.ToString("N0", CultureInfo.CurrentCulture) +
            TranslateUiText(" · сессия: ") + session.ToString("N0", CultureInfo.CurrentCulture);
        string cpu = _cpuMiningActive ? TranslateUiText("CPU: ") + FormatHashrate(_cpuHashrateValue) : "";
        string gpu = _gpuMiningActive ? TranslateUiText("GPU: ") + _gpuHashrateText.Replace("Скорость GPU: ", "", StringComparison.Ordinal) : "";
        _lockHashrate.Text = TranslateUiText("Хешрейт: ") + (string.Join(" · ", new[] { cpu, gpu }.Where(value => value.Length > 0)) is { Length: > 0 } rate ? rate : "—");
        string node = IsNodeRunning()
            ? (_rpcHealthy ? _nodeStatus : "Нода запущена, RPC не отвечает")
            : "Нода остановлена";
        _lockNodeStatus.Text = TranslateUiText("Состояние ноды: ") + TranslateUiText(node) +
            (IsNodeRunning() ? Environment.NewLine + TranslateUiText("Пиров: ") + _connections.ToString(CultureInfo.CurrentCulture) : "");
        _lockUnlockButton.Enabled = _pinLockoutUntil is null || DateTimeOffset.UtcNow >= _pinLockoutUntil;
        _lockHelloButton.Visible = _windowsHelloEnabled;
        if (_lockMessage.Text.Length == 0) _lockMessage.ForeColor = Muted;
        ApplyUiLanguage(_lockOverlay);
    }

    private void InitializeSystemNotifications()
    {
        _notificationMenu = new ContextMenuStrip();
        _trayOpenMenuItem = _notificationMenu.Items.Add(TranslateUiText("Открыть GeoCoin-Start"), null, (_, _) => RestoreFromSystemTray());
        _notificationMenu.Items.Add(new ToolStripSeparator());
        _trayExitMenuItem = _notificationMenu.Items.Add(TranslateUiText("Выйти из GeoCoin-Start"), null, (_, _) =>
        {
            _trayExitRequested = true;
            Close();
        });
        _systemNotificationIcon = new NotifyIcon
        {
            Text = "GeoCoin-Start",
            Icon = Icon ?? SystemIcons.Application,
            ContextMenuStrip = _notificationMenu,
            Visible = true
        };
        _systemNotificationIcon.DoubleClick += (_, _) => RestoreFromSystemTray();
    }

    private void HideToSystemTray()
    {
        if (IsPinLockConfigured()) LockApplication();
        ShowInTaskbar = false;
        Hide();
    }

    private void RestoreFromSystemTray()
    {
        ShowInTaskbar = true;
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Show();
        Activate();
        if (_applicationLocked) _lockPinInput.Focus();
    }

    private void UpdateSystemNotificationState()
    {
        if (_systemNotificationIcon is not null) _systemNotificationIcon.Visible = true;
    }

    private bool ShowSystemNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (!_notificationsEnabled || _systemNotificationIcon is null) return false;
        try
        {
            string localizedTitle = System.Security.SecurityElement.Escape(TranslateUiText(title)) ?? "GeoCoin-Start";
            string localizedMessage = System.Security.SecurityElement.Escape(TranslateUiText(message)) ?? "";
            string xml = "<toast><visual><binding template=\"ToastGeneric\"><text>" + localizedTitle +
                "</text><text>" + localizedMessage + "</text></binding></visual></toast>";
            var document = new Windows.Data.Xml.Dom.XmlDocument();
            document.LoadXml(xml);
            var notification = new Windows.UI.Notifications.ToastNotification(document);
            var notifier = Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier(AppPaths.ToastAppUserModelId);
            if (notifier.Setting != Windows.UI.Notifications.NotificationSetting.Enabled)
                throw new InvalidOperationException("Windows toast notifications are blocked: " + notifier.Setting);
            notification.Failed += (_, _) =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((Action)(() => ShowNotificationBalloon(title, message, icon)));
            };
            notifier.Show(notification);
            return true;
        }
        catch (Exception ex)
        {
            WriteNodeLog("Не удалось показать уведомление в центре уведомлений Windows: " + ex.Message);
            ShowNotificationBalloon(title, message, icon);
            return false;
        }
    }

    private void ShowNotificationBalloon(string title, string message, ToolTipIcon icon)
    {
        try
        {
            if (_systemNotificationIcon is null) return;
            _systemNotificationIcon.Visible = true;
            _systemNotificationIcon.BalloonTipTitle = TranslateUiText(title);
            _systemNotificationIcon.BalloonTipText = TranslateUiText(message);
            _systemNotificationIcon.BalloonTipIcon = icon;
            _systemNotificationIcon.ShowBalloonTip(8000);
        }
        catch { }
    }

    private void BuildOverviewPage()
    {
        int cardWidth = PageCardWidth(700);
        AddParagraph("Здесь видно состояние кошелька и синхронизации. Для начала запустите ноду.", 22, 12, cardWidth, 44);

        Panel balanceCard = AddCard(22, 72, cardWidth, 162);
        AddLabel(balanceCard, "БАЛАНС КОШЕЛЬКА", 24, 20, 250, 20, Muted, 9, bold: true);
        _balanceValue.Text = _nodeProcess is null ? "— GC" : FormatGcAmount(_balance);
        _balanceValue.Location = UiPoint(22, 49);
        _balanceValue.AutoSize = true;
        _balanceValue.Font = new Font("Segoe UI Semibold", 30, FontStyle.Bold);
        _balanceValue.ForeColor = Ink;
        balanceCard.Controls.Add(_balanceValue);
        _nodeState.Text = _nodeProcess is null ? "Нода не запущена" : _nodeStatus;
        _nodeState.Location = UiPoint(26, 112);
        _nodeState.AutoSize = true;
        _nodeState.ForeColor = Muted;
        balanceCard.Controls.Add(_nodeState);

        Panel networkCard = AddCard(22, 252, cardWidth, 142);
        AddLabel(networkCard, "СОСТОЯНИЕ СЕТИ", 24, 20, 230, 20, Muted, 9, bold: true);
        _syncState.Text = _nodeProcess is null ? "Запустите ноду, чтобы начать подключение" : SyncDescription();
        _syncState.Location = UiPoint(24, 53);
        _syncState.AutoSize = false;
        _syncState.Size = UiSize(Math.Max(240, cardWidth - 58), 28);
        _syncState.Font = new Font("Segoe UI Semibold", 13, FontStyle.Bold);
        _syncState.ForeColor = Ink;
        networkCard.Controls.Add(_syncState);
        _peerState.Text = _nodeProcess is null ? "" : FormatOverviewPeerState();
        _peerState.Location = UiPoint(26, 95);
        _peerState.AutoSize = true;
        _peerState.ForeColor = Muted;
        networkCard.Controls.Add(_peerState);

        Panel miningSummary = AddCard(22, 412, cardWidth, 120);
        AddLabel(miningSummary, "ДОБЫЧА И НАГРАДЫ", 24, 17, 260, 20, Muted, 9, bold: true);
        _lifetimeMinedBlocksLabel.Location = UiPoint(24, 47);
        _lifetimeMinedBlocksLabel.Size = UiSize(cardWidth - 60, 24);
        _lifetimeMinedBlocksLabel.Font = new Font("Segoe UI Semibold", 11, FontStyle.Bold);
        _lifetimeMinedBlocksLabel.ForeColor = Ink;
        miningSummary.Controls.Add(_lifetimeMinedBlocksLabel);
        _matureCoinsLabel.Location = UiPoint(24, 80);
        _matureCoinsLabel.Size = UiSize(Math.Max(220, cardWidth / 2 - 32), 22);
        _matureCoinsLabel.ForeColor = Muted;
        miningSummary.Controls.Add(_matureCoinsLabel);
        _immatureCoinsLabel.Location = UiPoint(Math.Max(250, cardWidth / 2), 80);
        _immatureCoinsLabel.Size = UiSize(Math.Max(220, cardWidth / 2 - 32), 22);
        _immatureCoinsLabel.ForeColor = Muted;
        miningSummary.Controls.Add(_immatureCoinsLabel);
        UpdateOverviewMiningSummary();

        _nodeButton.Text = _startingNode ? "Запускаю ноду…" : !IsNodeProcessRunning() ? "Запустить ноду" : "Остановить ноду";
        StylePrimaryButton(_nodeButton, new Point(22, 550), new Size(192, 42));
        _nodeButton.Enabled = !_remoteGpuMining && !_startingRemoteGpu && !_startingNode;
        _pagePanel.Controls.Add(_nodeButton);

        bool hasWallet = File.Exists(AppPaths.WalletPath);
        _walletButton.Text = hasWallet ? "Экспортировать кошелёк (.dat)" : "Создать кошелёк";
        StyleSecondaryButton(_walletButton, new Point(232, 550), new Size(230, 42));
        _walletButton.Enabled = IsNodeRunning();
        _pagePanel.Controls.Add(_walletButton);

        _walletRestoreButton.Text = hasWallet ? "Импортировать другой" : "Импортировать кошелёк";
        StyleSecondaryButton(_walletRestoreButton, new Point(232, 600), new Size(230, 42));
        _walletRestoreButton.Enabled = IsNodeRunning();
        _pagePanel.Controls.Add(_walletRestoreButton);

        _overviewHint.Text = File.Exists(AppPaths.WalletPath)
            ? "Сделайте резервную копию кошелька и храните её отдельно от компьютера."
            : "Создайте кошелёк или импортируйте существующий файл GeoCoin. Пароль хранится только у вас.";
        _overviewHint.Location = UiPoint(24, 658);
        _overviewHint.Size = UiSize(cardWidth - 24, 44);
        _overviewHint.ForeColor = Muted;
        _pagePanel.Controls.Add(_overviewHint);
    }

    private string FormatOverviewPeerState()
        => TranslateUiText("Блоков: ") + _height.ToString("N0", CultureInfo.CurrentCulture) +
            "     " + TranslateUiText("Узлов сети: ") + _connections.ToString(CultureInfo.CurrentCulture);

    private void BuildMiningPage()
    {
        int cardWidth = PageCardWidth(700);
        AddParagraph("CPU и GPU настраиваются в одном окне. Их можно запускать по отдельности или одновременно.", 22, 12, cardWidth, 44);
        Panel blockStats = AddCard(22, 68, cardWidth, 74);
        AddLabel(blockStats, "ДОБЫТЫЕ БЛОКИ", 22, 11, 240, 18, Muted, 8.5f, bold: true);
        _minedBlocksTodayLabel.Location = UiPoint(22, 36);
        _minedBlocksTodayLabel.Size = UiSize(Math.Max(220, cardWidth / 2 - 30), 24);
        _minedBlocksTodayLabel.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
        _minedBlocksTodayLabel.ForeColor = Ink;
        blockStats.Controls.Add(_minedBlocksTodayLabel);
        _minedBlocksSessionLabel.Location = UiPoint(Math.Max(250, cardWidth / 2), 36);
        _minedBlocksSessionLabel.Size = UiSize(Math.Max(220, cardWidth / 2 - 30), 24);
        _minedBlocksSessionLabel.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
        _minedBlocksSessionLabel.ForeColor = Ink;
        blockStats.Controls.Add(_minedBlocksSessionLabel);
        Panel card = AddCard(22, 154, cardWidth, 680);

        AddLabel(card, "Майнинг на процессоре · CPU", 24, 18, cardWidth - 48, 24, Ink, 13, bold: true);
        AddLabel(card, "Потоки", 24, 52, 100, 22, Muted, 10, bold: true);
        _threadCount.Location = UiPoint(24, 78);
        _threadCount.Size = UiSize(112, 31);
        _threadCount.Minimum = 1;
        _threadCount.Maximum = Math.Max(1, Environment.ProcessorCount);
        _threadCount.Value = Math.Max(1, Math.Min((int)_threadCount.Maximum, _selectedThreads));
        _threadCount.Font = new Font("Segoe UI", 11);
        _threadCount.Enabled = !_cpuMiningActive;
        card.Controls.Add(_threadCount);

        _miningState.Text = _cpuMiningActive
            ? $"CPU-майнинг включён · потоков: {_selectedThreads}"
            : "CPU-майнинг остановлен";
        _miningState.Location = UiPoint(24, 120);
        _miningState.AutoSize = true;
        _miningState.ForeColor = _cpuMiningActive ? Success : Muted;
        card.Controls.Add(_miningState);
        _cpuHashrate.Text = _cpuMiningActive
            ? $"Скорость CPU: {FormatHashrate(_cpuHashrateValue)}"
            : "Скорость CPU: —";
        _cpuHashrate.Location = UiPoint(24, 145);
        _cpuHashrate.Size = UiSize(cardWidth - 48, 24);
        _cpuHashrate.ForeColor = Muted;
        card.Controls.Add(_cpuHashrate);

        _mineButton.Text = _cpuMiningActive ? "Остановить CPU" : "Запустить CPU";
        StylePrimaryButton(_mineButton, new Point(24, 174), new Size(204, 42));
        _mineButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_mineButton);

        var divider = new Panel
        {
            Location = UiPoint(24, 232),
            Size = UiSize(cardWidth - 48, 1),
            BackColor = Divider
        };
        card.Controls.Add(divider);

        AddLabel(card, "Майнинг на видеокарте · GPU", 24, 250, cardWidth - 48, 24, Ink, 13, bold: true);
        AddLabel(card, "Награда поступит на уже существующий адрес этого кошелька.", 24, 280, cardWidth - 48, 24, Muted, 9.5f);
        AddLabel(card, "Протокол GPU", 24, 310, 150, 20, Ink, 10, bold: true);
        ConfigureGpuBackendCombo(_gpuBackendCombo, new Point(24, 334), new Size(Math.Max(240, cardWidth - 150), 34), !_gpuMiningActive);
        card.Controls.Add(_gpuBackendCombo);

        _refreshGpuButton.Text = "Найти";
        StyleSecondaryButton(_refreshGpuButton, new Point(cardWidth - 112, 332), new Size(88, 36));
        _refreshGpuButton.Enabled = !_gpuMiningActive && !_checkingGpuDevices;
        card.Controls.Add(_refreshGpuButton);

        AddLabel(card, "Видеокарты — можно выбрать несколько", 24, 374, cardWidth - 48, 20, Ink, 10, bold: true);
        ConfigureGpuDeviceList(_gpuDeviceList, new Point(24, 398), new Size(cardWidth - 48, 126), !_gpuMiningActive);
        card.Controls.Add(_gpuDeviceList);

        _gpuMiningState.Text = _gpuStatusText;
        _gpuMiningState.Location = UiPoint(24, 534);
        _gpuMiningState.AutoSize = true;
        _gpuMiningState.ForeColor = _gpuMiningActive ? Success : Muted;
        card.Controls.Add(_gpuMiningState);
        _miningHashrate.Text = _gpuHashrateText;
        _miningHashrate.Location = UiPoint(24, 558);
        _miningHashrate.Size = UiSize(cardWidth - 48, 24);
        _miningHashrate.ForeColor = Muted;
        card.Controls.Add(_miningHashrate);
        _gpuRewardAddress.Text = string.IsNullOrWhiteSpace(_lastAddress)
            ? "Адрес награды будет взят из текущего кошелька."
            : "Адрес награды: " + _lastAddress;
        _gpuRewardAddress.Location = UiPoint(24, 582);
        _gpuRewardAddress.Size = UiSize(cardWidth - 48, 24);
        _gpuRewardAddress.ForeColor = Muted;
        _gpuRewardAddress.AutoEllipsis = true;
        card.Controls.Add(_gpuRewardAddress);

        _gpuMineButton.Text = _remoteGpuMining ? "Перейти к подмайнеру" : _gpuMiningActive ? "Остановить GPU" : "Запустить GPU";
        StylePrimaryButton(_gpuMineButton, new Point(24, 616), new Size(_remoteGpuMining ? 240 : 204, 42));
        _gpuMineButton.Enabled = _remoteGpuMining || (IsNodeRunning() && File.Exists(AppPaths.WalletPath) &&
            (_gpuMiningActive || SelectedGpuDevices().Count > 0));
        card.Controls.Add(_gpuMineButton);

        if (!IsNodeRunning()) AddParagraph("Сначала запустите ноду на странице «Обзор».", 24, 850, cardWidth - 48, 32);
        else if (!File.Exists(AppPaths.WalletPath)) AddParagraph("Сначала создайте или импортируйте кошелёк на странице «Обзор».", 24, 850, cardWidth - 48, 32);
        UpdateMinedBlockCounters();
    }

    private void BuildNodePage()
    {
        int cardWidth = PageCardWidth(800);
        AddParagraph("Один процесс GeoCoin выполняет работу ноды. RPC — интерфейс управления этой же нодой; отдельно запускать вторую RPC-ноду не нужно.",
            22, 12, cardWidth, 48);

        Panel statusCard = AddCard(22, 70, cardWidth, 132);
        AddLabel(statusCard, "СОСТОЯНИЕ УЗЛА", 22, 15, 280, 20, Muted, 9, bold: true);
        _nodeProcessStatus.Location = UiPoint(22, 40);
        _nodeProcessStatus.Size = UiSize(Math.Max(230, cardWidth - 44), 26);
        _nodeProcessStatus.Font = new Font("Segoe UI Semibold", 14, FontStyle.Bold);
        statusCard.Controls.Add(_nodeProcessStatus);
        _nodeRpcStatus.Location = UiPoint(22, 72);
        _nodeRpcStatus.Size = UiSize(240, 22);
        _nodeRpcStatus.ForeColor = Muted;
        statusCard.Controls.Add(_nodeRpcStatus);
        _nodePeerStatus.Location = UiPoint(270, 72);
        _nodePeerStatus.Size = UiSize(Math.Max(190, cardWidth - 294), 22);
        _nodePeerStatus.ForeColor = Muted;
        statusCard.Controls.Add(_nodePeerStatus);
        _nodeButton.Text = _startingNode ? "Запускаю ноду…" : IsNodeRunning() ? "Остановить ноду" : "Запустить ноду";
        StylePrimaryButton(_nodeButton, new Point(22, 98), new Size(190, 34));
        _nodeButton.Enabled = !_remoteGpuMining && !_startingRemoteGpu && !_startingNode;
        statusCard.Controls.Add(_nodeButton);
        _nodeRuntimeStatus.Location = UiPoint(228, 104);
        _nodeRuntimeStatus.Size = UiSize(Math.Max(200, cardWidth - 250), 22);
        _nodeRuntimeStatus.ForeColor = Muted;
        statusCard.Controls.Add(_nodeRuntimeStatus);

        _nodePeerCard = AddCard(22, 216, cardWidth, 96);
        AddLabel(_nodePeerCard, "Пиры сети", 22, 12, cardWidth - 230, 20, Muted, 9, bold: true);
        _nodePeerSummary.Location = UiPoint(22, 40);
        _nodePeerSummary.Size = UiSize(cardWidth - 44, 22);
        _nodePeerSummary.ForeColor = Ink;
        _nodePeerSummary.AutoEllipsis = true;
        _nodePeerSummary.Cursor = Cursors.Help;
        _nodePeerCard.Controls.Add(_nodePeerSummary);
        _nodePeerToggleButton.Text = "Показать список";
        StyleSecondaryButton(_nodePeerToggleButton, new Point(cardWidth - 200, 6), new Size(178, 38));
        _nodePeerToggleButton.TextAlign = ContentAlignment.MiddleCenter;
        _nodePeerToggleButton.Click -= NodePeerToggleButtonClick;
        _nodePeerToggleButton.Click += NodePeerToggleButtonClick;
        _nodePeerCard.Controls.Add(_nodePeerToggleButton);
        _nodeP2pHelpLink.Text = "Как открыть P2P-порт";
        _nodeP2pHelpLink.Location = UiPoint(22, 68);
        _nodeP2pHelpLink.Size = UiSize(210, 22);
        _nodeP2pHelpLink.LinkColor = Accent;
        _nodeP2pHelpLink.ActiveLinkColor = Accent;
        _nodeP2pHelpLink.VisitedLinkColor = Accent;
        _nodeP2pHelpLink.LinkClicked -= NodeP2pHelpLinkClicked;
        _nodeP2pHelpLink.LinkClicked += NodeP2pHelpLinkClicked;
        _nodePeerCard.Controls.Add(_nodeP2pHelpLink);
        _nodePeerDetails.Location = UiPoint(22, 96);
        _nodePeerDetails.Size = UiSize(cardWidth - 44, 136);
        _nodePeerDetails.Multiline = true;
        _nodePeerDetails.ReadOnly = true;
        _nodePeerDetails.ScrollBars = ScrollBars.Vertical;
        _nodePeerDetails.WordWrap = true;
        _nodePeerDetails.BorderStyle = BorderStyle.FixedSingle;
        _nodePeerDetails.Font = new Font("Segoe UI", 9);
        _nodePeerDetails.BackColor = InputSurface;
        _nodePeerDetails.ForeColor = Ink;
        _nodePeerDetails.TabStop = false;
        _nodePeerCard.Controls.Add(_nodePeerDetails);
        _nodePeerHint.Location = UiPoint(22, 238);
        _nodePeerHint.Size = UiSize(cardWidth - 44, 30);
        _nodePeerHint.ForeColor = Muted;
        _nodePeerHint.AutoEllipsis = true;
        _nodePeerCard.Controls.Add(_nodePeerHint);

        _nodeRpcCard = AddCard(22, 314, cardWidth, 378);
        Panel rpcCard = _nodeRpcCard!;
        AddLabel(rpcCard, "ДОСТУП ДЛЯ GPU-ПОДМАЙНЕРА", 22, 15, cardWidth - 44, 20, Muted, 9, bold: true);
        AddLabel(rpcCard, "Адрес прослушивания", 22, 44, 230, 20, Ink, 10, bold: true);
        _rpcBindCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _rpcBindCombo.SelectedIndexChanged -= RpcBindComboSelectedIndexChanged;
        _rpcBindCombo.Items.Clear();
        _rpcBindCombo.Items.Add(TranslateUiText("127.0.0.1 · только этот компьютер"));
        int bindIndex = 0;
        for (int i = 1; i < _rpcBindCombo.Items.Count; i++)
            if (string.Equals(_rpcBindCombo.Items[i]?.ToString(), _rpcBindAddress, StringComparison.Ordinal)) bindIndex = i;
        if (bindIndex == 0) _rpcBindAddress = "127.0.0.1";
        _rpcBindCombo.SelectedIndex = bindIndex;
        _rpcBindCombo.Location = UiPoint(22, 68);
        _rpcBindCombo.Size = UiSize(cardWidth - 44, 32);
        _rpcBindCombo.Enabled = !IsNodeRunning() && !_startingNode;
        _rpcBindCombo.SelectedIndexChanged += RpcBindComboSelectedIndexChanged;
        rpcCard.Controls.Add(_rpcBindCombo);
        AddLabel(rpcCard, "Для доступа с другого компьютера используйте защищённый туннель к порту 3033 на этом компьютере.",
            22, 106, cardWidth - 44, 44, Muted, 8.5f);

        AddLabel(rpcCard, "Имя пользователя майнера", 22, 158, 250, 20, Ink, 10, bold: true);
        StyleTextBox(_minerRpcUserInput, new Point(22, 182), new Size(Math.Min(310, cardWidth - 44), 34));
        _minerRpcUserInput.Enabled = !IsNodeRunning() && !_startingNode;
        rpcCard.Controls.Add(_minerRpcUserInput);
        AddLabel(rpcCard, "Пароль майнера", 22, 226, 220, 20, Ink, 10, bold: true);
        int passwordWidth = Math.Min(370, cardWidth - 160);
        StyleTextBox(_minerRpcPasswordInput, new Point(22, 250), new Size(passwordWidth, 34));
        _minerRpcPasswordInput.Enabled = !IsNodeRunning() && !_startingNode;
        rpcCard.Controls.Add(_minerRpcPasswordInput);
        _toggleMinerPasswordVisibilityButton.Text = _minerRpcPasswordInput.UseSystemPasswordChar ? "Показать" : "Скрыть";
        StyleSecondaryButton(_toggleMinerPasswordVisibilityButton, new Point(passwordWidth + 34, 250), new Size(100, 34));
        rpcCard.Controls.Add(_toggleMinerPasswordVisibilityButton);
        _nodeProfileCopyButton.Text = "Скопировать данные для подмайнера";
        StyleSecondaryButton(_nodeProfileCopyButton, new Point(22, 296), new Size(Math.Min(310, cardWidth - 44), 34));
        rpcCard.Controls.Add(_nodeProfileCopyButton);
        _nodeProfileHint.Text = _rpcBindAddress == "127.0.0.1"
            ? "Для майнинга с другого компьютера подключитесь через защищённый туннель."
            : "Пароль действует, пока приложение открыто. Если появится запрос брандмауэра, разрешите только частные сети.";
        _nodeProfileHint.Location = UiPoint(22, 338);
        _nodeProfileHint.Size = UiSize(cardWidth - 44, 32);
        _nodeProfileHint.ForeColor = Muted;
        rpcCard.Controls.Add(_nodeProfileHint);

        _nodeLogCard = AddCard(22, 706, cardWidth, 430);
        Panel logCard = _nodeLogCard!;
        AddLabel(logCard, "ЖУРНАЛ НОДЫ И МАЙНЕРА", 22, 15, 320, 20, Muted, 9, bold: true);
        _pauseNodeLogUpdates.Text = "Пауза отображения журнала";
        _pauseNodeLogUpdates.Location = UiPoint(22, 45);
        _pauseNodeLogUpdates.Size = UiSize(cardWidth - 44, 24);
        _pauseNodeLogUpdates.AutoSize = false;
        _pauseNodeLogUpdates.ForeColor = Ink;
        _pauseNodeLogUpdates.BackColor = Surface;
        logCard.Controls.Add(_pauseNodeLogUpdates);
        _nodeDiagnosticsCopyButton.Text = "Копировать отчёт";
        StyleSecondaryButton(_nodeDiagnosticsCopyButton, new Point(22, 76), new Size(190, 32));
        logCard.Controls.Add(_nodeDiagnosticsCopyButton);
        _nodeLogOpenButton.Text = "Открыть файл журнала";
        StyleSecondaryButton(_nodeLogOpenButton, new Point(222, 76), new Size(190, 32));
        logCard.Controls.Add(_nodeLogOpenButton);
        _nodeLogBox.Location = UiPoint(18, 116);
        _nodeLogBox.Size = UiSize(cardWidth - 38, 296);
        _nodeLogBox.Multiline = true;
        _nodeLogBox.ReadOnly = true;
        _nodeLogBox.ScrollBars = ScrollBars.Both;
        _nodeLogBox.WordWrap = false;
        _nodeLogBox.Font = new Font("Consolas", 8.5f);
        _nodeLogBox.BackColor = Color.FromArgb(25, 31, 28);
        _nodeLogBox.ForeColor = Color.FromArgb(218, 226, 216);
        _nodeLogBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        logCard.Controls.Add(_nodeLogBox);
        UpdateNodePeerCardLayout(cardWidth);
        UpdateNodePage();
    }

    private void BuildSubminerPage()
    {
        int cardWidth = PageCardWidth(800);
        AddParagraph("Этот компьютер будет работать как GPU-подмайнер и подключится к уже запущенной ноде на другом ПК. Локальную ноду запускать не нужно.",
            22, 12, cardWidth, 48);
        Panel blockStats = AddCard(22, 70, cardWidth, 74);
        AddLabel(blockStats, "ДОБЫТЫЕ БЛОКИ", 22, 11, 240, 18, Muted, 8.5f, bold: true);
        _minedBlocksTodayLabel.Location = UiPoint(22, 36);
        _minedBlocksTodayLabel.Size = UiSize(Math.Max(220, cardWidth / 2 - 30), 24);
        _minedBlocksTodayLabel.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
        _minedBlocksTodayLabel.ForeColor = Ink;
        blockStats.Controls.Add(_minedBlocksTodayLabel);
        _minedBlocksSessionLabel.Location = UiPoint(Math.Max(250, cardWidth / 2), 36);
        _minedBlocksSessionLabel.Size = UiSize(Math.Max(220, cardWidth / 2 - 30), 24);
        _minedBlocksSessionLabel.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
        _minedBlocksSessionLabel.ForeColor = Ink;
        blockStats.Controls.Add(_minedBlocksSessionLabel);
        Panel card = AddCard(22, 156, cardWidth, 654);
        AddLabel(card, "ПОДКЛЮЧЕНИЕ К НОДЕ", 22, 16, cardWidth - 44, 20, Muted, 9, bold: true);
        AddLabel(card, "Адрес RPC-ноды", 22, 48, 230, 20, Ink, 10, bold: true);
        StyleTextBox(_remoteNodeUrlInput, new Point(22, 72), new Size(Math.Min(470, cardWidth - 44), 34));
        _remoteNodeUrlInput.Enabled = !_remoteGpuMining && !_startingRemoteGpu;
        card.Controls.Add(_remoteNodeUrlInput);
        AddLabel(card, "Имя пользователя майнера", 22, 122, 260, 20, Ink, 10, bold: true);
        StyleTextBox(_remoteMinerUserInput, new Point(22, 146), new Size(Math.Min(300, cardWidth - 44), 34));
        _remoteMinerUserInput.Enabled = !_remoteGpuMining && !_startingRemoteGpu;
        card.Controls.Add(_remoteMinerUserInput);
        AddLabel(card, "Пароль роли майнера", 22, 194, 240, 20, Ink, 10, bold: true);
        int remotePassWidth = Math.Min(370, cardWidth - 160);
        StyleTextBox(_remoteMinerPasswordInput, new Point(22, 218), new Size(remotePassWidth, 34));
        _remoteMinerPasswordInput.Enabled = !_remoteGpuMining && !_startingRemoteGpu;
        card.Controls.Add(_remoteMinerPasswordInput);
        _toggleRemotePasswordVisibilityButton.Text = _remoteMinerPasswordInput.UseSystemPasswordChar ? "Показать" : "Скрыть";
        StyleSecondaryButton(_toggleRemotePasswordVisibilityButton, new Point(remotePassWidth + 34, 218), new Size(100, 34));
        card.Controls.Add(_toggleRemotePasswordVisibilityButton);
        AddLabel(card, "Адрес для награды", 22, 266, 260, 20, Ink, 10, bold: true);
        StyleTextBox(_remoteRewardAddressInput, new Point(22, 290), new Size(Math.Min(520, cardWidth - 44), 34));
        _remoteRewardAddressInput.Enabled = !_remoteGpuMining && !_startingRemoteGpu;
        card.Controls.Add(_remoteRewardAddressInput);
        AddLabel(card, _remoteGpuMining ? "Остановите подмайнер, чтобы сменить протокол GPU" : "Протокол GPU",
            22, 338, cardWidth - 44, 20, Ink, 10, bold: true);
        ConfigureGpuBackendCombo(_remoteGpuBackendCombo, new Point(22, 362), new Size(Math.Max(240, cardWidth - 160), 34), !_remoteGpuMining && !_startingRemoteGpu);
        card.Controls.Add(_remoteGpuBackendCombo);
        _remoteGpuFindButton.Text = "Найти";
        StyleSecondaryButton(_remoteGpuFindButton, new Point(cardWidth - 122, 360), new Size(96, 36));
        _remoteGpuFindButton.Enabled = !_remoteGpuMining && !_startingRemoteGpu && !_checkingGpuDevices;
        card.Controls.Add(_remoteGpuFindButton);
        AddLabel(card, "Видеокарты — можно выбрать несколько", 22, 402, cardWidth - 44, 20, Ink, 10, bold: true);
        ConfigureGpuDeviceList(_remoteGpuDeviceList, new Point(22, 426), new Size(cardWidth - 44, 126), !_remoteGpuMining && !_startingRemoteGpu);
        card.Controls.Add(_remoteGpuDeviceList);
        _gpuMiningState.Text = _gpuStatusText;
        _gpuMiningState.Location = UiPoint(22, 560);
        _gpuMiningState.AutoSize = true;
        _gpuMiningState.ForeColor = _remoteGpuMining ? Success : Muted;
        card.Controls.Add(_gpuMiningState);
        _miningHashrate.Text = _gpuHashrateText;
        _miningHashrate.Location = UiPoint(330, 560);
        _miningHashrate.Size = UiSize(Math.Max(180, cardWidth - 360), 24);
        _miningHashrate.ForeColor = Muted;
        card.Controls.Add(_miningHashrate);
        _remoteGpuButton.Text = _remoteGpuMining ? "Остановить подмайнер" : _startingRemoteGpu ? "Подключаюсь…" : "Подключить и начать GPU-майнинг";
        StylePrimaryButton(_remoteGpuButton, new Point(22, 596), new Size(300, 36));
        _remoteGpuButton.Enabled = _remoteGpuMining || (!_startingRemoteGpu && !_checkingGpuDevices && !IsNodeRunning() &&
            _gpuMinerProcess is null && SelectedGpuDevices().Count > 0);
        card.Controls.Add(_remoteGpuButton);
        AddParagraph("Укажите локальный порт защищённого туннеля и учётные данные miner RPC.",
            22, 832, cardWidth, 48);
        Panel rpcLogCard = AddCard(22, 888, cardWidth, 246);
        AddLabel(rpcLogCard, "ЖУРНАЛ RPC ПОДМАЙНЕРА", 22, 16, cardWidth - 44, 22, Muted, 9, bold: true);
        _subminerRpcLogBox.AccessibleName = TranslateUiText("ЖУРНАЛ RPC ПОДМАЙНЕРА");
        _subminerRpcLogBox.Location = UiPoint(22, 50);
        _subminerRpcLogBox.Size = UiSize(cardWidth - 44, 176);
        _subminerRpcLogBox.Multiline = true;
        _subminerRpcLogBox.ReadOnly = true;
        _subminerRpcLogBox.ScrollBars = ScrollBars.Vertical;
        rpcLogCard.Controls.Add(_subminerRpcLogBox);
        RefreshSubminerRpcLog();
        UpdateMinedBlockCounters();
    }

    private void RpcBindComboSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_rpcBindCombo.SelectedIndex == 0 || _rpcBindCombo.SelectedItem is null)
            _rpcBindAddress = "127.0.0.1";
        else _rpcBindAddress = _rpcBindCombo.SelectedItem.ToString() ?? "127.0.0.1";
        _nodeProfileHint.Text = _rpcBindAddress == "127.0.0.1"
            ? "Для майнинга с другого компьютера подключитесь через защищённый туннель."
            : "Пароль действует, пока приложение открыто. Если появится запрос брандмауэра, разрешите только частные сети.";
    }

    private void UpdateNodePage()
    {
        if (IsDisposed || _currentPage != "node") return;
        bool running = IsNodeRunning();
        _nodeProcessStatus.Text = running
            ? _nodeStatus
            : (_startingNode ? "Нода запускается…" : _startingRemoteGpu ? "Подключаю GPU к удалённой ноде…" :
                _remoteGpuMining ? "Локальная нода остановлена · работает режим подмайнера" : "Нода остановлена");
        _nodeProcessStatus.ForeColor = running ? Success : Muted;
        _nodeRpcStatus.Text = running ? (_rpcHealthy ? "RPC отвечает" : "Процесс запущен · проверяю RPC…") : "RPC не запущен";
        _nodeRpcStatus.ForeColor = _rpcHealthy ? Success : Muted;
        _nodePeerStatus.Text = running ? $"Блоков: {_height:N0} · подключений: {_connections}" : "Сеть: —";
        if (!running)
        {
            _nodePeerDiagnostics.Clear();
            _peerDiagnosticsAvailable = false;
            _peerListAvailable = false;
            _lastPeerDiagnosticsRefreshUtc = DateTime.MinValue;
        }
        else if (!_peerDiagnosticsLoading && DateTime.UtcNow - _lastPeerDiagnosticsRefreshUtc >= TimeSpan.FromSeconds(12))
        {
            _ = RefreshPeerDiagnosticsAsync();
        }
        UpdateNodePeerDisplay(running);
        string runTime = "";
        if (_nodeProcess is { } process && IsNodeProcessRunning())
        {
            try { runTime = $"PID {process.Id} · запущена в {process.StartTime.ToString("HH:mm:ss", UiLocalization.Culture(_uiLanguage))}"; }
            catch { runTime = $"PID {process.Id}"; }
        }
        _nodeRuntimeStatus.Text = runTime;
        if (_nodeButton.IsHandleCreated)
        {
            _nodeButton.Text = _startingNode ? "Запускаю ноду…" : running ? "Остановить ноду" : "Запустить ноду";
            _nodeButton.Enabled = !_remoteGpuMining && !_startingRemoteGpu && !_startingNode;
        }
        if (_nodeProfileCopyButton.IsHandleCreated)
            _nodeProfileCopyButton.Enabled = running;
        if (_nodeLogBox.IsHandleCreated && !_pauseNodeLogUpdates.Checked)
        {
            string content = ReadNodeLogTail();
            if (!string.Equals(content, _nodeLogBox.Text, StringComparison.Ordinal))
            {
                _nodeLogBox.Text = content;
                _nodeLogBox.SelectionStart = _nodeLogBox.TextLength;
                _nodeLogBox.ScrollToCaret();
            }
        }
    }

    private void NodePeerToggleButtonClick(object? sender, EventArgs e)
    {
        _nodePeersExpanded = !_nodePeersExpanded;
        UpdateNodePeerCardLayout(PageCardWidth(800));
        UpdateNodePeerDisplay(IsNodeRunning());
        _pagePanel.PerformLayout();
    }

    private void UpdateNodePeerCardLayout(int cardWidth)
    {
        if (_nodePeerCard is null) return;
        int peerCardHeight = _nodePeersExpanded ? 276 : 96;
        _nodePeerCard.Size = UiSize(cardWidth, peerCardHeight);
        _nodePeerDetails.Visible = _nodePeersExpanded;
        _nodePeerHint.Visible = _nodePeersExpanded;
        if (_nodeRpcCard is not null)
            _nodeRpcCard.Location = UiPoint(22, 216 + peerCardHeight + 14);
        if (_nodeLogCard is not null)
            _nodeLogCard.Location = UiPoint(22, 216 + peerCardHeight + 14 + 378 + 14);
    }

    private void UpdateNodePeerDisplay(bool running)
    {
        if (_nodePeerCard is null || _nodePeerCard.IsDisposed) return;
        _nodePeerToggleButton.Text = TranslateUiText(_nodePeersExpanded ? "Скрыть список" : "Показать список");

        if (!running)
        {
            _nodePeerSummary.Text = TranslateUiText("Запустите ноду, чтобы увидеть состояние пиров.");
            _nodePeerToolTip.SetToolTip(_nodePeerSummary, TranslateUiText("Запустите ноду, чтобы увидеть состояние пиров."));
            _nodePeerDetails.Text = TranslateUiText("Запустите ноду, чтобы увидеть состояние пиров.");
            _nodePeerHint.Text = TranslateUiText("Запустите ноду, чтобы увидеть состояние пиров.");
            return;
        }

        int active = _peerListAvailable ? _nodePeerDiagnostics.Count(peer => peer.Connected) : _connections;
        string portRefused = _peerDiagnosticsAvailable
            ? _nodePeerDiagnostics.Count(peer => peer.PortRefused).ToString(CultureInfo.InvariantCulture)
            : "—";
        string otherFailures = _peerDiagnosticsAvailable
            ? _nodePeerDiagnostics.Count(peer => peer.OtherFailure).ToString(CultureInfo.InvariantCulture)
            : "—";
        _nodePeerSummary.Text = TranslateUiText("Активные: ") + active.ToString(CultureInfo.InvariantCulture) +
            " · " + TranslateUiText("Отказ порта*: ") + portRefused +
            " · " + TranslateUiText("Другие сбои: ") + otherFailures;
        _nodePeerToolTip.SetToolTip(_nodePeerSummary, TranslateUiText(_peerDiagnosticsAvailable
            ? "Отказ соединения часто указывает на закрытый порт или блокировку брандмауэром. Таймаут сам по себе не подтверждает, что порт закрыт."
            : "Подробная диагностика недоступна. Показаны только активные подключения."));

        if (!_peerListAvailable)
        {
            _nodePeerDetails.Text = _peerDiagnosticsLoading
                ? TranslateUiText("Читаю состояние пиров…")
                : TranslateUiText("Не удалось получить список пиров. Проверьте журнал ноды.");
            _nodePeerHint.Text = _peerDiagnosticsLoading
                ? TranslateUiText("Читаю состояние пиров…")
                : TranslateUiText("Не удалось получить список пиров. Проверьте журнал ноды.");
            return;
        }

        if (_nodePeerDiagnostics.Count == 0)
        {
            _nodePeerDetails.Text = TranslateUiText("Нет активных пиров и недавних попыток подключения.");
        }
        else
        {
            var lines = new List<string>(_nodePeerDiagnostics.Count);
            foreach (PeerDiagnostic peer in _nodePeerDiagnostics)
            {
                string state;
                if (peer.Connected) state = TranslateUiText("Подключено");
                else if (peer.InProgress) state = TranslateUiText("Подключается");
                else if (peer.PortRefused) state = TranslateUiText("Порт отклоняет соединение");
                else state = TranslateUiText("Другая ошибка соединения");

                string line = state + " · " + peer.Address;
                if (peer.Connected)
                {
                    line += " · " + TranslateUiText(peer.Inbound ? "Входящее" : "Исходящее");
                    line += " · " + TranslateUiText(peer.Encrypted ? "Зашифровано" : "Без шифрования");
                    if (peer.PingMilliseconds > 0)
                        line += " · " + TranslateUiText("Ping: ") + peer.PingMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms";
                    if (!string.IsNullOrWhiteSpace(peer.State))
                        line += " · " + TranslateUiText("Состояние: ") + peer.State;
                }
                else
                {
                    if (peer.FailureCount > 0)
                        line += " · " + TranslateUiText("попыток: ") + peer.FailureCount.ToString(CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(peer.Error))
                    {
                        string error = peer.Error.Replace('\r', ' ').Replace('\n', ' ').Trim();
                        if (error.Length > 180) error = error[..180] + "…";
                        line += " · " + TranslateUiText("Ошибка: ") + error;
                    }
                }
                lines.Add(line);
            }
            _nodePeerDetails.Text = string.Join(Environment.NewLine, lines);
        }

        _nodePeerHint.Text = _peerDiagnosticsAvailable
            ? TranslateUiText("Отказ соединения часто указывает на закрытый порт или блокировку брандмауэром. Таймаут сам по себе не подтверждает, что порт закрыт.")
            : TranslateUiText("Подробная диагностика недоступна. Показаны только активные подключения.");
    }

    private async Task RefreshPeerDiagnosticsAsync()
    {
        GeoCoinRpcClient? rpc = _rpc;
        if (_peerDiagnosticsLoading || rpc is null || !IsNodeRunning()) return;
        _peerDiagnosticsLoading = true;
        _lastPeerDiagnosticsRefreshUtc = DateTime.UtcNow;
        try
        {
            try
            {
                JsonElement diagnostics = await rpc.CallAsync("getconnectiondiagnostics");
                if (!ReferenceEquals(_rpc, rpc)) return;
                if (!diagnostics.TryGetProperty("peers", out JsonElement peers) || peers.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException("Peer diagnostics are not available.");

                var parsed = ParseConnectedPeers(peers);
                bool hasAttempts = diagnostics.TryGetProperty("outbound_attempts", out JsonElement attempts) &&
                    attempts.ValueKind == JsonValueKind.Array;
                if (hasAttempts)
                {
                    Dictionary<string, (int Code, DateTimeOffset Timestamp)> recentNetworkErrors = ParseRecentConnectErrors(ReadNodeLogTail());
                    ParseOutboundAttempts(attempts, parsed, recentNetworkErrors);
                }
                _nodePeerDiagnostics.Clear();
                _nodePeerDiagnostics.AddRange(parsed);
                _peerDiagnosticsAvailable = hasAttempts;
                _peerListAvailable = true;
            }
            catch
            {
                JsonElement peers = await rpc.CallAsync("getpeerinfo");
                if (!ReferenceEquals(_rpc, rpc)) return;
                if (peers.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException("Peer list is not available.");
                _nodePeerDiagnostics.Clear();
                _nodePeerDiagnostics.AddRange(ParseConnectedPeers(peers));
                _peerDiagnosticsAvailable = false;
                _peerListAvailable = true;
            }
        }
        catch
        {
            if (ReferenceEquals(_rpc, rpc))
            {
                _nodePeerDiagnostics.Clear();
                _peerDiagnosticsAvailable = false;
                _peerListAvailable = false;
            }
        }
        finally
        {
            _peerDiagnosticsLoading = false;
            if (!IsDisposed && _currentPage == "node") UpdateNodePeerDisplay(IsNodeRunning());
        }
    }

    private static List<PeerDiagnostic> ParseConnectedPeers(JsonElement peers)
    {
        var results = new List<PeerDiagnostic>();
        if (peers.ValueKind != JsonValueKind.Array) return results;
        foreach (JsonElement peer in peers.EnumerateArray())
        {
            string address = ReadString(peer, "addr");
            if (string.IsNullOrWhiteSpace(address)) continue;
            results.Add(new PeerDiagnostic(address, true, false, false, false,
                ReadBool(peer, "inbound"), ReadBool(peer, "transport_encrypted"), ReadString(peer, "state"),
                (int)Math.Clamp(Math.Round(ReadDouble(peer, "ping_rtt_ms")), 0, int.MaxValue), 0, ""));
        }
        return results;
    }

    private static void ParseOutboundAttempts(JsonElement attempts, List<PeerDiagnostic> results,
        IReadOnlyDictionary<string, (int Code, DateTimeOffset Timestamp)> recentNetworkErrors)
    {
        var connectedAddresses = new HashSet<string>(results.Select(peer => peer.Address), StringComparer.OrdinalIgnoreCase);
        if (attempts.ValueKind != JsonValueKind.Array) return;
        foreach (JsonElement attempt in attempts.EnumerateArray())
        {
            string address = ReadString(attempt, "addr");
            if (string.IsNullOrWhiteSpace(address) || connectedAddresses.Contains(address)) continue;
            int failureCount = (int)Math.Clamp(ReadInt64(attempt, "fail_count"), 0, int.MaxValue);
            bool inProgress = ReadBool(attempt, "in_progress");
            if (failureCount <= 0 && !inProgress) continue;
            long lastAttemptAge = ReadInt64(attempt, "last_attempt_age_ms");
            bool inBackoff = ReadBool(attempt, "in_backoff");
            if (lastAttemptAge > TimeSpan.FromHours(24).TotalMilliseconds && !inBackoff && !inProgress) continue;
            string error = ReadString(attempt, "last_error");
            if (recentNetworkErrors.TryGetValue(address, out var logFailure))
            {
                long attemptAgeMs = Math.Min(Math.Max(0L, lastAttemptAge), (long)TimeSpan.FromDays(7).TotalMilliseconds);
                DateTimeOffset expectedFailureTime = DateTimeOffset.UtcNow.AddMilliseconds(-attemptAgeMs);
                if (Math.Abs((logFailure.Timestamp - expectedFailureTime).TotalMinutes) <= 10)
                    error = $"error={logFailure.Code}";
            }
            bool portRefused = failureCount > 0 && IsConnectionRefused(error);
            results.Add(new PeerDiagnostic(address, false, portRefused, failureCount > 0 && !portRefused,
                inProgress, false, false, "", 0, failureCount, error));
        }
    }

    private static Dictionary<string, (int Code, DateTimeOffset Timestamp)> ParseRecentConnectErrors(string logText)
    {
        const string marker = "Connect failed for ";
        const string errorMarker = " (error=";
        var errors = new Dictionary<string, (int Code, DateTimeOffset Timestamp)>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in logText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int addressStart = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (addressStart < 0) continue;
            addressStart += marker.Length;
            int errorStart = line.IndexOf(errorMarker, addressStart, StringComparison.OrdinalIgnoreCase);
            if (errorStart <= addressStart) continue;
            int codeStart = errorStart + errorMarker.Length;
            int codeEnd = line.IndexOf(')', codeStart);
            if (codeEnd < 0) continue;
            string address = line[addressStart..errorStart].Trim();
            int timestampEnd = line.IndexOf(']');
            DateTimeOffset timestamp = DateTimeOffset.MinValue;
            if (line.Length > 0 && line[0] == '[' && timestampEnd > 1)
                DateTimeOffset.TryParse(line.AsSpan(1, timestampEnd - 1), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out timestamp);
            if (int.TryParse(line.AsSpan(codeStart, codeEnd - codeStart), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int code) && !string.IsNullOrWhiteSpace(address) && timestamp != DateTimeOffset.MinValue)
                errors[address] = (code, timestamp);
        }
        return errors;
    }

    private static bool IsConnectionRefused(string error)
    {
        if (error.Contains("refused", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("ECONNREFUSED", StringComparison.OrdinalIgnoreCase)) return true;
        int marker = error.IndexOf("error=", StringComparison.OrdinalIgnoreCase);
        if (marker < 0) marker = error.IndexOf("errno=", StringComparison.OrdinalIgnoreCase);
        if (marker < 0) return false;
        marker += 6;
        int end = marker;
        while (end < error.Length && char.IsDigit(error[end])) end++;
        return int.TryParse(error.AsSpan(marker, end - marker), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out int code) && code is 10061 or 111;
    }

    private void NodeP2pHelpLinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        (string Title, string Message) help = _uiLanguage switch
        {
            "en" => ("Help the GeoCoin network", "To accept inbound peers and help the network:\n\n" +
                "1. Keep GeoCoin-Start and the node running. The degree mainnet uses TCP port 3030.\n" +
                "2. In Windows Defender Firewall, add an inbound TCP rule for port 3030 (or allow geocoin-node.exe) on Private networks.\n" +
                "3. If your PC is behind a router, reserve its private LAN IP and forward external TCP 3030 to that PC on TCP 3030.\n" +
                "4. Your ISP must provide a publicly reachable IP. CGNAT or double NAT usually prevents inbound connections; ask your ISP for a public IP.\n" +
                "5. Never forward RPC port 3033 to the public Internet. Use it only on this PC or a trusted LAN/VPN.\n\n" +
                "Leave the node online, then check incoming peers in this list. A port check can succeed only while the node is running."),
            "zh-CN" => ("帮助 GeoCoin 网络", "要接受入站节点并帮助网络：\n\n" +
                "1. 保持 GeoCoin-Start 和节点运行。degree 主网使用 TCP 端口 3030。\n" +
                "2. 在 Windows Defender 防火墙中添加 TCP 3030 入站规则（或允许 geocoin-node.exe），仅限专用网络。\n" +
                "3. 如果电脑连接到路由器，请为电脑保留固定的局域网 IP，并将外部 TCP 3030 转发到电脑的 TCP 3030。\n" +
                "4. 互联网服务提供商必须提供可从公网访问的 IP。CGNAT 或双重 NAT 通常会阻止入站连接；请向服务商申请公网 IP。\n" +
                "5. 切勿将 RPC 端口 3033 转发到公网。仅在本机或可信 LAN/VPN 中使用。\n\n" +
                "保持节点在线，然后在此列表中查看入站节点。只有节点运行时，端口检查才有效。"),
            "pt-BR" => ("Ajude a rede GeoCoin", "Para aceitar conexões de entrada e ajudar a rede:\n\n" +
                "1. Mantenha o GeoCoin-Start e o nó em execução. A rede principal degree usa a porta TCP 3030.\n" +
                "2. No Firewall do Windows Defender, crie uma regra de entrada TCP para a porta 3030 (ou permita geocoin-node.exe), somente em redes privadas.\n" +
                "3. Se o PC estiver atrás de um roteador, reserve um IP privado fixo para ele e encaminhe a porta TCP externa 3030 para a TCP 3030 do PC.\n" +
                "4. Seu provedor precisa fornecer um IP acessível publicamente. CGNAT ou NAT duplo geralmente impede conexões de entrada; solicite um IP público ao provedor.\n" +
                "5. Nunca encaminhe a porta RPC 3033 para a Internet pública. Use-a somente neste PC ou em uma LAN/VPN confiável.\n\n" +
                "Deixe o nó online e confira os peers de entrada nesta lista. O teste de porta só funciona enquanto o nó está em execução."),
            "it" => ("Aiuta la rete GeoCoin", "Per accettare peer in ingresso e aiutare la rete:\n\n" +
                "1. Tieni GeoCoin-Start e il nodo in esecuzione. La mainnet degree usa la porta TCP 3030.\n" +
                "2. In Windows Defender Firewall crea una regola TCP in ingresso per la porta 3030 (oppure consenti geocoin-node.exe), solo sulle reti private.\n" +
                "3. Se il PC è collegato a un router, assegna un IP LAN privato fisso e inoltra la porta TCP esterna 3030 alla TCP 3030 del PC.\n" +
                "4. Il provider deve fornire un IP raggiungibile pubblicamente. CGNAT o doppio NAT di solito impediscono le connessioni in ingresso; chiedi un IP pubblico al provider.\n" +
                "5. Non inoltrare mai la porta RPC 3033 su Internet. Usala solo su questo PC o su una LAN/VPN fidata.\n\n" +
                "Lascia il nodo online e controlla i peer in ingresso in questo elenco. Il controllo della porta funziona solo mentre il nodo è attivo."),
            _ => ("Поддержать сеть GeoCoin", "Чтобы принимать входящие подключения и помогать сети:\n\n" +
                "1. Оставьте GeoCoin-Start и ноду запущенными. В основной сети degree используется TCP-порт 3030.\n" +
                "2. В брандмауэре Защитника Windows создайте входящее правило для TCP-порта 3030 (или разрешите geocoin-node.exe) только для частных сетей.\n" +
                "3. Если компьютер подключён через роутер, закрепите за ним частный IP-адрес и настройте проброс внешнего TCP-порта 3030 на TCP-порт 3030 этого компьютера.\n" +
                "4. Провайдер должен выдавать публичный IP-адрес. CGNAT или двойной NAT обычно мешают входящим подключениям; запросите у провайдера публичный IP.\n" +
                "5. Не перенаправляйте RPC-порт 3033 в интернет. Используйте его только на этом компьютере или в доверенной LAN/VPN.\n\n" +
                "Оставьте ноду в сети и проверьте входящие подключения в списке пиров. Проверка порта снаружи работает только при запущенной ноде.")
        };
        ShowLocalizedMessage(help.Message, help.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static List<string> GetPrivateIpv4Addresses()
    {
        var addresses = new SortedSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                foreach (UnicastIPAddressInformation item in nic.GetIPProperties().UnicastAddresses)
                {
                    IPAddress address = item.Address;
                    if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || IPAddress.IsLoopback(address)) continue;
                    byte[] bytes = address.GetAddressBytes();
                    bool privateAddress = bytes[0] == 10 ||
                        (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                        (bytes[0] == 192 && bytes[1] == 168);
                    if (privateAddress) addresses.Add(address.ToString());
                }
            }
        }
        catch { }
        return addresses.ToList();
    }

    private string ReadNodeLogTail()
    {
        if (!File.Exists(AppPaths.LogPath)) return "Журнал появится после запуска ноды.";
        try
        {
            using var stream = new FileStream(AppPaths.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            const int maxBytes = 96 * 1024;
            if (stream.Length > maxBytes) stream.Seek(-maxBytes, SeekOrigin.End);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string[] lines = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(Environment.NewLine, lines.TakeLast(300));
        }
        catch (IOException) { return "Не удалось прочитать журнал. Попробуйте открыть файл отдельно."; }
    }

    private void OpenNodeLog()
    {
        Directory.CreateDirectory(AppPaths.DataRoot);
        if (!File.Exists(AppPaths.LogPath)) File.WriteAllText(AppPaths.LogPath, "Журнал GeoCoin-Start" + Environment.NewLine);
        Process.Start(new ProcessStartInfo { FileName = AppPaths.LogPath, UseShellExecute = true });
    }

    private void CopyNodeDiagnostics()
    {
        bool processRunning = IsNodeProcessRunning();
        string rpcStatus = IsNodeRunning() ? (_rpcHealthy ? "Connected" : "Checking") : processRunning ? "Unavailable" : "Not running";
        string gpuMining = _remoteGpuMining ? "Running (remote subminer)" : _gpuMiningActive ? "Running (local)" : "Stopped";
        string devices = _gpuDevicesScanned ? _gpuDevices.Count.ToString(CultureInfo.InvariantCulture) : "Not scanned";
        string report = string.Join(Environment.NewLine,
            "GeoCoin-Start diagnostic summary",
            $"Generated (UTC): {DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)}",
            $"App version: {AppPaths.AppVersion}",
            $"OS: Windows {Environment.OSVersion.Version}",
            $"Architecture: {(Environment.Is64BitProcess ? "x64" : "x86")}",
            $"Official node package: {(File.Exists(AppPaths.NodePath) ? "Installed" : "Missing")}",
            $"Local node process: {(processRunning ? "Running" : "Stopped")}",
            $"RPC status: {rpcStatus}",
            "Network: degree",
            $"Block height: {(processRunning ? _height.ToString("N0", CultureInfo.InvariantCulture) : "Not available")}",
            $"Known headers: {(processRunning ? _headers.ToString("N0", CultureInfo.InvariantCulture) : "Not available")}",
            $"Connected peers: {(processRunning ? _connections.ToString(CultureInfo.InvariantCulture) : "Not available")}",
            $"CPU mining: {(_cpuMiningActive ? "Running" : "Stopped")}",
            $"GPU mining: {gpuMining}",
            $"GPU devices detected: {devices}");

        try
        {
            Clipboard.SetText(report);
            ShowLocalizedMessage(
                "Диагностическая сводка скопирована. Она содержит только технический статус, без адресов кошелька и паролей.",
                "Диагностика", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch
        {
            ShowLocalizedMessage("Не удалось скопировать сводку в буфер обмена.", "Диагностика",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CopyMinerConnectionProfile()
    {
        if (!IsNodeRunning() || string.IsNullOrWhiteSpace(_minerPassword))
        {
            ShowLocalizedMessage("Сначала запустите ноду на странице «Нода».",
                "Нет удалённого доступа", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string profile = $"RPC на компьютере с нодой: http://127.0.0.1:3033/{Environment.NewLine}" +
                         $"Пользователь майнера: {_minerRpcUsername}{Environment.NewLine}" +
                         $"Пароль майнера: {_minerPassword}{Environment.NewLine}" +
                         "На другом компьютере укажите локальный порт защищённого туннеля и эти учётные данные.";
        Clipboard.SetText(profile);
        ShowLocalizedMessage("Данные роли майнера скопированы. Передавайте их через защищённый канал.",
            "Профиль скопирован", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BuildSendPage()
    {
        int cardWidth = PageCardWidth(700);
        int fieldWidth = cardWidth - 60;
        string pageHint = !IsNodeRunning()
            ? "Нода остановлена. Можно подготовить адрес и сумму; для отправки сначала запустите ноду на странице «Обзор»."
            : !File.Exists(AppPaths.WalletPath)
                ? "Откройте или создайте кошелёк на странице «Обзор», чтобы отправлять монеты."
                : "Введите адрес получателя или загрузите QR-код. Перед отправкой проверьте сумму и оценку комиссии.";
        AddParagraph(pageHint, 22, 12, cardWidth, 48);
        Panel card = AddCard(22, 78, cardWidth, 454);
        AddLabel(card, "Адрес получателя", 26, 18, fieldWidth, 22, Ink, 11, bold: true);
        int addressInputWidth = Math.Max(220, fieldWidth - 160);
        StyleTextBox(_sendAddress, new Point(26, 47), new Size(addressInputWidth, 36));
        _sendAddress.PlaceholderText = "Адрес GeoCoin получателя";
        card.Controls.Add(_sendAddress);
        _importPaymentQrButton.Text = "Импортировать QR";
        StyleSecondaryButton(_importPaymentQrButton, new Point(cardWidth - 164, 47), new Size(138, 36));
        _importPaymentQrButton.Enabled = true;
        card.Controls.Add(_importPaymentQrButton);

        AddLabel(card, "Сумма, GC", 26, 99, 200, 22, Ink, 11, bold: true);
        StyleTextBox(_sendAmount, new Point(26, 128), new Size(Math.Min(220, fieldWidth), 36));
        _sendAmount.PlaceholderText = "Например, 1,25";
        card.Controls.Add(_sendAmount);

        _sendFeeEstimate.Text = IsNodeRunning() ? "Рассчитываю оценку комиссии…" : "Комиссия появится после запуска ноды.";
        _sendFeeEstimate.Location = UiPoint(26, 177);
        _sendFeeEstimate.Size = UiSize(fieldWidth, 24);
        _sendFeeEstimate.ForeColor = Ink;
        _sendFeeEstimate.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
        card.Controls.Add(_sendFeeEstimate);
        _sendFeeHint.Text = "Сумма ориентировочная; GeoCoin рассчитывает комиссию по размеру транзакции.";
        _sendFeeHint.Location = UiPoint(26, 202);
        _sendFeeHint.Size = UiSize(fieldWidth, 42);
        _sendFeeHint.ForeColor = Muted;
        _sendFeeHint.Font = new Font("Segoe UI", 9);
        card.Controls.Add(_sendFeeHint);

        AddLabel(card, "Пароль кошелька", 26, 249, 220, 22, Ink, 11, bold: true);
        StyleTextBox(_sendPassword, new Point(26, 278), new Size(Math.Min(320, fieldWidth), 36));
        _sendPassword.UseSystemPasswordChar = true;
        _sendPassword.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_sendPassword);

        _sendButton.Text = "Проверить и отправить";
        StylePrimaryButton(_sendButton, new Point(26, 329), new Size(228, 42));
        _sendButton.Enabled = !_sendingTransfer && IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_sendButton);

        _sendMessage.Location = UiPoint(26, 379);
        _sendMessage.Size = UiSize(fieldWidth, 48);
        _sendMessage.ForeColor = Muted;
        card.Controls.Add(_sendMessage);
        if (IsNodeRunning() && File.Exists(AppPaths.WalletPath)) _ = RefreshSendFeeEstimateAsync();
    }

    private void BuildReceivePage()
    {
        if (!IsNodeRunning() || !File.Exists(AppPaths.WalletPath))
        {
            BuildUnavailableState(
                !IsNodeRunning() ? "Нода остановлена" : "Кошелёк не открыт",
                !IsNodeRunning()
                    ? "Чтобы показать адреса и QR-код, сначала запустите ноду. Затем создайте или откройте кошелёк."
                    : "Чтобы получить монеты, создайте новый кошелёк или импортируйте существующий на странице «Обзор».");
            return;
        }

        int cardWidth = PageCardWidth(700);
        bool compact = cardWidth < 650;
        int addressWidth = Math.Max(220, cardWidth - 260);
        AddParagraph("Выберите существующий адрес или создайте новый. Покажите его отправителю или дайте отсканировать QR-код.", 22, 12, cardWidth, 48);
        Panel card = AddCard(22, 78, cardWidth, compact ? 390 : 322);
        AddLabel(card, "АДРЕС ДЛЯ ПОЛУЧЕНИЯ", 26, 20, addressWidth, 22, Muted, 9, bold: true);
        _receiveAddressCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _receiveAddressCombo.Location = UiPoint(26, 50);
        _receiveAddressCombo.Size = UiSize(addressWidth, 34);
        _receiveAddressCombo.Font = new Font("Segoe UI", 9.5f);
        _receiveAddressCombo.Enabled = !_loadingReceiveAddresses && IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_receiveAddressCombo);

        _receiveAddress.Location = UiPoint(26, 96);
        _receiveAddress.Size = UiSize(addressWidth, 60);
        _receiveAddress.AutoEllipsis = true;
        _receiveAddress.ForeColor = Ink;
        _receiveAddress.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
        _receiveAddress.TextAlign = ContentAlignment.MiddleLeft;
        card.Controls.Add(_receiveAddress);

        _receiveButton.Text = "Создать новый адрес";
        StylePrimaryButton(_receiveButton, new Point(26, 168), new Size(190, 42));
        _receiveButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_receiveButton);

        _receiveCopyButton.Text = "Копировать адрес";
        StyleSecondaryButton(_receiveCopyButton,
            new Point(compact ? 26 : 230, compact ? 218 : 168), new Size(180, 42));
        _receiveCopyButton.Enabled = !string.IsNullOrWhiteSpace(_lastAddress);
        card.Controls.Add(_receiveCopyButton);

        _saveReceiveQrButton.Text = "Сохранить QR-код";
        StyleSecondaryButton(_saveReceiveQrButton,
            new Point(26, compact ? 268 : 218), new Size(180, 38));
        _saveReceiveQrButton.Enabled = !string.IsNullOrWhiteSpace(_lastAddress);
        card.Controls.Add(_saveReceiveQrButton);

        _receiveMessage.Text = IsNodeRunning() && File.Exists(AppPaths.WalletPath)
            ? "Загружаю адреса текущего кошелька…"
            : "Адреса появятся после запуска ноды и открытия кошелька.";
        _receiveMessage.Location = UiPoint(26, compact ? 318 : 270);
        _receiveMessage.Size = UiSize(addressWidth, compact ? 56 : 42);
        _receiveMessage.ForeColor = Muted;
        card.Controls.Add(_receiveMessage);

        _receiveQrCode.Location = UiPoint(cardWidth - 190, 54);
        _receiveQrCode.Size = UiSize(150, 150);
        _receiveQrCode.SizeMode = PictureBoxSizeMode.Zoom;
        _receiveQrCode.BackColor = Color.White;
        card.Controls.Add(_receiveQrCode);
        _receiveQrCaption.Text = "QR содержит только адрес";
        _receiveQrCaption.Location = UiPoint(cardWidth - 196, 211);
        _receiveQrCaption.Size = UiSize(162, 38);
        _receiveQrCaption.TextAlign = ContentAlignment.TopCenter;
        _receiveQrCaption.ForeColor = Muted;
        _receiveQrCaption.Font = new Font("Segoe UI", 8.5f);
        card.Controls.Add(_receiveQrCaption);
        UpdateReceiveAddressDisplay();
    }

    private void BuildTransactionsPage()
    {
        _transactionsAutoLoadPending = false;
        if (!IsNodeRunning() || !File.Exists(AppPaths.WalletPath))
        {
            BuildUnavailableState(
                !IsNodeRunning() ? "Нода остановлена" : "Кошелёк не открыт",
                !IsNodeRunning()
                    ? "История появится после запуска ноды и подключения кошелька. Запустите ноду на странице «Обзор»."
                    : "Создайте или импортируйте кошелёк на странице «Обзор», чтобы загрузить историю переводов и наград.");
            return;
        }

        _transactionsAutoLoadPending = true;

        int cardWidth = PageCardWidth(700);
        AddParagraph("История входящих переводов, отправок и наград за майнинг.", 22, 12, Math.Max(160, cardWidth - 154), 38);
        _refreshTransactionsButton.Text = "Обновить";
        StyleSecondaryButton(_refreshTransactionsButton, new Point(cardWidth - 112, 8), new Size(88, 34));
        _refreshTransactionsButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath) && !_loadingTransactions;
        _pagePanel.Controls.Add(_refreshTransactionsButton);

        Panel card = AddCard(22, 58, cardWidth, 550);
        _transactionGrid = new DataGridView
        {
            Location = UiPoint(14, 14),
            Size = UiSize(cardWidth - 30, 400),
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            GridColor = Color.FromArgb(226, 226, 220),
            EnableHeadersVisualStyles = false,
            Font = new Font("Segoe UI", 9),
            RowTemplate = { Height = 30 }
        };
        _transactionGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 241, 235);
        _transactionGrid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
        _transactionGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
        _transactionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "date", HeaderText = "Дата", FillWeight = 125 });
        _transactionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "type", HeaderText = "Операция", FillWeight = 92 });
        _transactionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "amount", HeaderText = "Сумма", FillWeight = 105 });
        _transactionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "fee", HeaderText = "Комиссия", FillWeight = 78 });
        _transactionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "status", HeaderText = "Состояние", FillWeight = 112 });
        _transactionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "address", HeaderText = "Адрес", FillWeight = 178 });
        _transactionGrid.SelectionChanged += (_, _) =>
        {
            _selectedTransactionId = _transactionGrid?.CurrentRow?.Tag as string;
            _transactionDetails.Text = string.IsNullOrWhiteSpace(_selectedTransactionId)
                ? "Выберите запись, чтобы посмотреть номер транзакции."
                : "ID транзакции: " + _selectedTransactionId;
            _copyTransactionIdButton.Enabled = !string.IsNullOrWhiteSpace(_selectedTransactionId);
            UpdateTransactionExplorerLink();
        };
        card.Controls.Add(_transactionGrid);

        _transactionDetails.Text = "Выберите запись, чтобы посмотреть номер транзакции.";
        _transactionDetails.Location = UiPoint(18, 426);
        _transactionDetails.Size = UiSize(Math.Max(120, cardWidth - 190), 32);
        _transactionDetails.ForeColor = Muted;
        _transactionDetails.AutoEllipsis = true;
        card.Controls.Add(_transactionDetails);

        _copyTransactionIdButton.Text = "Копировать ID";
        StyleSecondaryButton(_copyTransactionIdButton, new Point(cardWidth - 154, 420), new Size(132, 34));
        _copyTransactionIdButton.Enabled = false;
        card.Controls.Add(_copyTransactionIdButton);

        _openTransactionExplorerLink.Text = TranslateUiText("Открыть в блокчейн-обозревателе ↗");
        _openTransactionExplorerLink.Location = UiPoint(18, 458);
        _openTransactionExplorerLink.Size = UiSize(Math.Min(340, Math.Max(160, cardWidth - 36)), 30);
        _openTransactionExplorerLink.BackColor = Color.Transparent;
        _openTransactionExplorerLink.BorderStyle = BorderStyle.None;
        _openTransactionExplorerLink.TextAlign = ContentAlignment.MiddleLeft;
        _openTransactionExplorerLink.LinkColor = Accent;
        _openTransactionExplorerLink.ActiveLinkColor = Accent;
        _openTransactionExplorerLink.VisitedLinkColor = Accent;
        _openTransactionExplorerLink.LinkBehavior = LinkBehavior.HoverUnderline;
        _openTransactionExplorerLink.Visible = false;
        _openTransactionExplorerLink.LinkClicked += (_, _) => OpenSelectedTransactionInExplorer();
        card.Controls.Add(_openTransactionExplorerLink);

        _transactionStatus.Text = !IsNodeRunning()
            ? "Сначала запустите ноду на странице «Обзор»."
            : !File.Exists(AppPaths.WalletPath)
                ? "Создайте или импортируйте кошелёк, чтобы увидеть историю."
                : "Запрашиваю историю кошелька…";
        _transactionStatus.Location = UiPoint(18, 500);
        int paginationLabelWidth = 64;
        int pageSizeWidth = 62;
        int pageButtonWidth = 38;
        int pageNumberWidth = 52;
        int paginationGap = 8;
        int paginationWidth = paginationLabelWidth + pageSizeWidth + pageButtonWidth + pageNumberWidth + pageButtonWidth + paginationGap * 4;
        int paginationLeft = cardWidth - 14 - paginationWidth;
        _transactionStatus.Size = UiSize(Math.Max(110, paginationLeft - 30), 28);
        _transactionStatus.ForeColor = Muted;
        _transactionStatus.AutoEllipsis = true;
        card.Controls.Add(_transactionStatus);

        _transactionsPageSizeLabel.Text = "Записей:";
        _transactionsPageSizeLabel.Location = UiPoint(paginationLeft, 500);
        _transactionsPageSizeLabel.Size = UiSize(paginationLabelWidth, 28);
        _transactionsPageSizeLabel.ForeColor = Muted;
        _transactionsPageSizeLabel.Font = new Font("Segoe UI", 8.5f);
        _transactionsPageSizeLabel.AutoEllipsis = true;
        card.Controls.Add(_transactionsPageSizeLabel);

        _transactionsPageSizeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        if (_transactionsPageSizeCombo.Items.Count == 0)
            _transactionsPageSizeCombo.Items.AddRange(new object[] { 15, 30, 50 });
        _transactionsPageSizeCombo.Location = UiPoint(paginationLeft + paginationLabelWidth + paginationGap, 496);
        _transactionsPageSizeCombo.Size = UiSize(pageSizeWidth, 32);
        _transactionsPageSizeCombo.Font = new Font("Segoe UI", 9);
        if (_transactionsPageSizeCombo.SelectedItem is not int)
            _transactionsPageSizeCombo.SelectedItem = _transactionsPageSize;
        card.Controls.Add(_transactionsPageSizeCombo);

        _previousTransactionsPageButton.Text = "‹";
        _previousTransactionsPageButton.AccessibleName = TranslateUiText("Предыдущая страница");
        int previousLeft = paginationLeft + paginationLabelWidth + paginationGap + pageSizeWidth + paginationGap;
        StyleSecondaryButton(_previousTransactionsPageButton, new Point(previousLeft, 496), new Size(pageButtonWidth, 32));
        card.Controls.Add(_previousTransactionsPageButton);
        int pageLabelLeft = previousLeft + pageButtonWidth + paginationGap;
        _transactionsPageLabel.Location = UiPoint(pageLabelLeft, 500);
        _transactionsPageLabel.Size = UiSize(pageNumberWidth, 24);
        _transactionsPageLabel.TextAlign = ContentAlignment.MiddleCenter;
        _transactionsPageLabel.ForeColor = Muted;
        card.Controls.Add(_transactionsPageLabel);
        _nextTransactionsPageButton.Text = "›";
        _nextTransactionsPageButton.AccessibleName = TranslateUiText("Следующая страница");
        int nextLeft = pageLabelLeft + pageNumberWidth + paginationGap;
        StyleSecondaryButton(_nextTransactionsPageButton, new Point(nextLeft, 496), new Size(pageButtonWidth, 32));
        card.Controls.Add(_nextTransactionsPageButton);
        RenderTransactionsPage();
    }

    private void BuildUnavailableState(string heading, string explanation)
    {
        int cardWidth = PageCardWidth(700);
        AddParagraph("На этой странице пока нет данных.", 22, 12, cardWidth, 40);
        Panel card = AddCard(22, 66, cardWidth, 226);
        AddLabel(card, heading, 26, 28, cardWidth - 64, 34, Ink, 18, bold: true);
        AddLabel(card, explanation, 26, 76, cardWidth - 64, 58, Muted, 10);
        var openOverview = new Button { Text = "Открыть обзор" };
        StylePrimaryButton(openOverview, new Point(26, 154), new Size(192, 42));
        openOverview.Click += (_, _) => SetPage("overview");
        card.Controls.Add(openOverview);
    }

    private void BuildWalletPage()
    {
        if (!IsNodeRunning() || !File.Exists(AppPaths.WalletPath))
        {
            BuildUnavailableState(
                !IsNodeRunning() ? "Нода остановлена" : "Кошелёк не открыт",
                !IsNodeRunning()
                    ? "Для управления кошельком запустите ноду на странице «Обзор»."
                    : "Создайте или импортируйте кошелёк на странице «Обзор», чтобы управлять резервными копиями и ключами.");
            return;
        }

        int cardWidth = PageCardWidth(700);
        int contentWidth = cardWidth - 48;
        AddParagraph("Резервная копия сохраняет весь кошелёк. Приватный ключ относится только к одному адресу.", 22, 12, cardWidth, 42);
        Panel card = AddCard(22, 66, cardWidth, 670);

        AddLabel(card, "Полная копия кошелька", 24, 18, contentWidth, 24, Ink, 13, bold: true);
        AddLabel(card, "Файл .dat включает весь текущий кошелёк и подходит для его восстановления.",
            24, 48, contentWidth, 38, Muted, 9.5f);
        _walletFileExportButton.Text = "Экспортировать весь кошелёк (.dat)";
        StylePrimaryButton(_walletFileExportButton, new Point(24, 88), new Size(268, 40));
        _walletFileExportButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_walletFileExportButton);

        var divider = new Panel
        {
            Location = UiPoint(24, 145),
            Size = UiSize(contentWidth, 1),
            BackColor = Divider
        };
        card.Controls.Add(divider);

        AddLabel(card, "Приватный ключ одного адреса", 24, 162, contentWidth, 24, Ink, 13, bold: true);
        AddLabel(card,
            "В GeoCoin нет мнемонической фразы. Приватный ключ из 64 hex-символов открывает один адрес.",
            24, 191, contentWidth, 44, Muted, 9.5f);

        AddLabel(card, "Адрес для экспорта ключа", 24, 240, contentWidth, 20, Ink, 10, bold: true);
        _walletAddressCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _walletAddressCombo.Location = UiPoint(24, 264);
        _walletAddressCombo.Size = UiSize(Math.Max(260, cardWidth - 160), 34);
        _walletAddressCombo.Font = new Font("Segoe UI", 9.5f);
        _walletAddressCombo.Enabled = !_loadingWalletAddresses;
        card.Controls.Add(_walletAddressCombo);

        _refreshWalletAddressesButton.Text = "Обновить";
        StyleSecondaryButton(_refreshWalletAddressesButton, new Point(cardWidth - 112, 262), new Size(88, 36));
        _refreshWalletAddressesButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath) && !_loadingWalletAddresses;
        card.Controls.Add(_refreshWalletAddressesButton);

        _walletKeyExportButton.Text = "Сохранить ключ выбранного адреса";
        StyleSecondaryButton(_walletKeyExportButton, new Point(24, 306), new Size(268, 38));
        _walletKeyExportButton.Enabled = IsNodeRunning() && _walletAddressCombo.SelectedItem is string;
        card.Controls.Add(_walletKeyExportButton);

        AddLabel(card, "Импорт ключа в текущий кошелёк", 24, 358, contentWidth, 20, Ink, 10, bold: true);
        StyleTextBox(_privateKeyInput, new Point(24, 382), new Size(Math.Max(230, cardWidth - 300), 36));
        _privateKeyInput.PlaceholderText = "64 шестнадцатеричных символа";
        _privateKeyInput.UseSystemPasswordChar = true;
        card.Controls.Add(_privateKeyInput);

        _walletKeyImportButton.Text = "Импортировать ключ";
        StylePrimaryButton(_walletKeyImportButton, new Point(cardWidth - 240, 380), new Size(216, 40));
        _walletKeyImportButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_walletKeyImportButton);

        _walletKeyStatus.Text = "Ключи — секретные данные. Не отправляйте их другим людям и не храните в общедоступных файлах.";
        _walletKeyStatus.Location = UiPoint(24, 440);
        _walletKeyStatus.Size = UiSize(contentWidth, 48);
        _walletKeyStatus.ForeColor = Muted;
        card.Controls.Add(_walletKeyStatus);

        var passwordDivider = new Panel
        {
            Location = UiPoint(24, 506),
            Size = UiSize(contentWidth, 1),
            BackColor = Divider
        };
        card.Controls.Add(passwordDivider);

        AddLabel(card, "Защита паролем кошелька", 24, 524, contentWidth, 24, Ink, 13, bold: true);
        AddLabel(card,
            "Задайте пароль для незашифрованного кошелька или замените текущий пароль. После изменения сохраните новую резервную копию.",
            24, 554, contentWidth, 42, Muted, 9.5f);
        _walletPasswordChangeButton.Text = "Изменить пароль кошелька";
        StyleSecondaryButton(_walletPasswordChangeButton, new Point(24, 608), new Size(268, 40));
        _walletPasswordChangeButton.Enabled = IsNodeRunning();
        card.Controls.Add(_walletPasswordChangeButton);
    }

    private async Task ChangeWalletPasswordAsync()
    {
        if (!IsNodeRunning() || _rpc is null || !File.Exists(AppPaths.WalletPath))
        {
            ShowLocalizedMessage("Смена пароля требует запущенной локальной ноды и открытого кошелька.",
                "Кошелёк не открыт", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _walletPasswordChangeButton.Enabled = false;
        try
        {
            JsonElement walletInfo = await _rpc.CallAsync("getwalletinfo");
            if (!walletInfo.TryGetProperty("encrypted", out JsonElement encryptedElement) ||
                encryptedElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new InvalidOperationException(TranslateUiText("Не удалось определить, защищён ли кошелёк паролем. Обновите состояние ноды и попробуйте снова."));

            bool walletIsEncrypted = encryptedElement.GetBoolean();
            if (!walletIsEncrypted && ShowLocalizedMessage(
                    "Этот кошелёк пока не защищён паролем. Установка пароля зашифрует его и перезапустит ноду; работающий майнинг остановится. Перед продолжением создайте резервную копию. Продолжить?",
                    "Задать пароль кошелька", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            WalletPasswordChange? change = WalletPasswordChangeDialog.Ask(this, walletIsEncrypted, _darkTheme, TranslateUiText);
            if (change is null) return;

            if (walletIsEncrypted)
            {
                await _rpc.CallAsync("walletpassphrasechange", new[] { change.CurrentPassword!, change.NewPassword });
                ShowLocalizedMessage(
                    "Пароль кошелька изменён. Новые резервные копии будут открываться новым паролем; старые копии сохраняют прежний пароль.",
                    "Пароль изменён", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await _rpc.CallAsync("encryptwallet", new[] { change.NewPassword });
            await StopNodeAsync();
            if (IsNodeRunning())
            {
                ShowLocalizedMessage(
                    "Пароль задан, кошелёк зашифрован, но нода ещё работает. Остановите её и запустите снова, введя новый пароль.",
                    "Кошелёк зашифрован", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await StartNodeAsync(offerWalletCreation: false, suppliedWalletPassword: change.NewPassword);
            if (!IsNodeRunning())
            {
                ShowLocalizedMessage(
                    "Пароль кошелька задан, но нода не смогла запуститься автоматически. Запустите её со страницы «Обзор» и введите новый пароль.",
                    "Пароль задан", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await RefreshStateAsync();
            ShowLocalizedMessage(
                "Пароль задан, кошелёк зашифрован и нода перезапущена. Создайте новую резервную копию и храните пароль отдельно. Старые копии не изменены.",
                "Кошелёк защищён", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (GeoCoinRpcException ex) when (ex.Code == -14)
        {
            ShowLocalizedMessage("Неверный пароль кошелька. Проверьте его и попробуйте снова.",
                "Неверный пароль", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            ShowLocalizedMessage(ex.Message, "Не удалось изменить пароль кошелька", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!_walletPasswordChangeButton.IsDisposed)
                _walletPasswordChangeButton.Enabled = IsNodeRunning();
        }
    }

    private async Task StartNodeAsync(bool offerWalletCreation = true, string? suppliedWalletPassword = null)
    {
        if (_startingNode) return;
        if (_remoteGpuMining || _startingRemoteGpu)
        {
            ShowLocalizedMessage("Сначала остановите GPU-подмайнер. В этом режиме приложение подключено к ноде на другом компьютере.",
                "Сначала остановите подмайнер", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!File.Exists(AppPaths.NodePath))
        {
            ShowLocalizedMessage("Файл ноды GeoCoin не найден. Запустите установщик ещё раз.",
                "GeoCoin-Start", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (_nodeProcess is not null && !IsNodeProcessRunning()) CleanupNodeSession();
        if (IsNodeProcessRunning()) return;

        _minerRpcUsername = _minerRpcUserInput.Text.Trim();
        string minerPassword = _minerRpcPasswordInput.Text;
        _minerPassword = minerPassword;
        if (string.IsNullOrWhiteSpace(_minerRpcUsername) || _minerRpcUsername.Contains(':') || _minerRpcUsername.Any(char.IsWhiteSpace))
        {
            ShowLocalizedMessage("Имя miner RPC должно быть непустым и не содержать пробелов или двоеточия.",
                "Проверьте имя пользователя", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.Equals(_minerRpcUsername, "geocoin-start", StringComparison.OrdinalIgnoreCase))
        {
            ShowLocalizedMessage("Для подмайнера задайте отдельное имя пользователя, отличное от admin RPC.",
                "Имя занято", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (minerPassword.Length < 16 || minerPassword.Contains('\r') || minerPassword.Contains('\n'))
        {
            ShowLocalizedMessage("Задайте для miner RPC пароль не короче 16 символов без переводов строки.",
                "Проверьте пароль", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string? walletPassword = null;
        bool existingWallet = File.Exists(AppPaths.WalletPath);
        if (existingWallet)
        {
            walletPassword = suppliedWalletPassword ?? PasswordDialog.AskToUnlock(this, TranslateUiText);
            if (walletPassword is null) return;
        }

        long nodeLogStartOffset = GetFileLengthOrZero(AppPaths.LogPath);
        bool retryWalletPassword = false;
        string sessionDirectory = Path.Combine(AppPaths.NodeSessionDirectory, Guid.NewGuid().ToString("N"));
        string? walletFile = null;
        _startingNode = true;
        _nodeStatus = "Запускаю ноду…";
        _rpcHealthy = false;
        UpdateStatus();
        BuildPage();
        try
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            Directory.CreateDirectory(AppPaths.NodeSessionDirectory);
            var sessionSecurity = new System.Security.AccessControl.DirectorySecurity();
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var user = identity.User ?? throw new InvalidOperationException("Не удалось определить пользователя Windows.");
            sessionSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            sessionSecurity.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                user, System.Security.AccessControl.FileSystemRights.FullControl,
                System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                System.Security.AccessControl.PropagationFlags.None, System.Security.AccessControl.AccessControlType.Allow));
            new DirectoryInfo(sessionDirectory).Create(sessionSecurity);
            _adminPassword = RandomSecret();
            _sessionDirectory = sessionDirectory;
            string adminFile = Path.Combine(sessionDirectory, "rpc-admin.pass");
            _minerPassFile = Path.Combine(sessionDirectory, "rpc-miner.pass");
            // The upstream narrow getenv() can lose non-ASCII characters on Windows.
            bool usePasswordEnvironment = walletPassword is { Length: > 0 } &&
                walletPassword.All(character => char.IsAscii(character) && character is not ('\0' or '\r' or '\n'));
            if (existingWallet && !usePasswordEnvironment)
                walletFile = Path.Combine(sessionDirectory, "wallet.pass");
            var secretEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            await File.WriteAllTextAsync(adminFile, _adminPassword + Environment.NewLine, secretEncoding);
            await File.WriteAllTextAsync(_minerPassFile, minerPassword + Environment.NewLine, secretEncoding);
            if (walletFile is not null) await File.WriteAllTextAsync(walletFile, walletPassword + Environment.NewLine, secretEncoding);

            var start = new ProcessStartInfo
            {
                FileName = NodeCompatiblePath(AppPaths.NodePath),
                WorkingDirectory = NodeCompatiblePath(AppPaths.OfficialDirectory),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            AddArgument(start, "--data-dir", NodeCompatiblePath(AppPaths.DataRoot));
            AddArgument(start, "--network", AppPaths.NetworkId);
            start.ArgumentList.Add("--rpc");
            AddArgument(start, "--rpc-bind", "127.0.0.1");
            start.Environment.Remove("GEOCOIN_ALLOW_PUBLIC_RPC_BIND");
            AddArgument(start, "--rpc-port", "3033");
            AddArgument(start, "--rpc-user", "geocoin-start");
            AddArgument(start, "--rpc-pass-file", NodeCompatiblePath(adminFile));
            AddArgument(start, "--rpc-miner-user", _minerRpcUsername);
            AddArgument(start, "--rpc-miner-pass-file", NodeCompatiblePath(_minerPassFile));
            AddArgument(start, "--activation-manifest", NodeCompatiblePath(AppPaths.ProductionManifestPath));
            AddArgument(start, "--status-interval", "30");
            start.Environment.Remove("GEOCOIN_WALLET_PASS");
            if (usePasswordEnvironment) start.Environment["GEOCOIN_WALLET_PASS"] = walletPassword!;
            if (walletFile is not null) AddArgument(start, "--wallet-password-file", NodeCompatiblePath(walletFile));

            _logWriter?.Dispose();
            _logWriter = new StreamWriter(new FileStream(AppPaths.LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            { AutoFlush = true };
            _nodeProcess = new Process { StartInfo = start, EnableRaisingEvents = true };
            _nodeProcess.OutputDataReceived += (_, e) => WriteNodeLog(e.Data);
            _nodeProcess.ErrorDataReceived += (_, e) => WriteNodeLog(e.Data);
            try
            {
                if (!_nodeProcess.Start()) throw new InvalidOperationException("Windows не запустил ноду GeoCoin.");
            }
            finally
            {
                start.Environment.Remove("GEOCOIN_WALLET_PASS");
            }
            _nodeProcess.BeginOutputReadLine();
            _nodeProcess.BeginErrorReadLine();

            _rpc?.Dispose();
            const string localRpcEndpoint = "http://127.0.0.1:3033/";
            _rpc = new GeoCoinRpcClient("geocoin-start", _adminPassword, localRpcEndpoint);
            _minerRpc?.Dispose();
            _minerRpc = new GeoCoinRpcClient(_minerRpcUsername, _minerPassword!, localRpcEndpoint);

            bool ready = false;
            for (int attempt = 0; attempt < 120; attempt++)
            {
                if (!IsNodeProcessRunning())
                    throw new InvalidOperationException("Нода завершила запуск с ошибкой. Подробности записаны в файл журнала.");
                try
                {
                    await _rpc.CallAsync("help");
                    ready = true;
                    _rpcHealthy = true;
                    break;
                }
                catch { await Task.Delay(1000); }
            }
            if (walletFile is not null) File.Delete(walletFile);
            if (!ready) throw new TimeoutException("Нода запускается дольше обычного. Проверьте файл журнала и попробуйте ещё раз.");

            _nodeStatus = "Нода подключена";
            _refreshTimer.Start();
            await RefreshStateAsync();
            if (!existingWallet && offerWalletCreation)
            {
                var result = ShowLocalizedMessage("Нода подключена. Создать новый защищённый кошелёк?\n\n" +
                    "Если у вас уже есть кошелёк GeoCoin, сначала остановите ноду и восстановите резервную копию. Не создавайте новый поверх старого файла.",
                    "Первый запуск", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result == DialogResult.Yes) await CreateWalletAsync();
            }
        }
        catch (Exception ex)
        {
            if (_nodeProcess is Process failedNode)
            {
                try
                {
                    if (!failedNode.HasExited)
                    {
                        failedNode.Kill(entireProcessTree: true);
                        await failedNode.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    }
                    failedNode.WaitForExit();
                }
                catch { }
            }
            try { _logWriter?.Flush(); } catch { }
            bool stopped = !IsNodeProcessRunning();
            if (stopped)
            {
                if (_nodeProcess is not null) CleanupNodeSession();
                else
                {
                    try { Directory.Delete(sessionDirectory, recursive: true); } catch { }
                    _sessionDirectory = null;
                    _minerPassFile = null;
                    _minerPassword = null;
                    _adminPassword = null;
                    ReleaseNodePathAliasIfIdle();
                }
                _nodeStatus = "Нода не подключена";
                _rpcHealthy = false;
            }
            else _nodeStatus = "Нода продолжает работать";
            if (existingWallet && NodeLogContainsWalletLoadFailure(nodeLogStartOffset))
            {
                DialogResult retry = ShowLocalizedMessage(
                    "Не удалось открыть кошелёк. Возможная причина — неверный пароль. Проверьте пароль и раскладку клавиатуры. Если пароль верен, возможно, файл кошелька повреждён. Исходный файл оставлен без изменений.",
                    "Не удалось открыть кошелёк", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                retryWalletPassword = retry == DialogResult.Retry;
            }
            else
            {
                ShowLocalizedMessage(ex.Message + "\n\nЖурнал: " + AppPaths.LogPath,
                    "Не удалось запустить GeoCoin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            if (walletFile is not null)
            {
                try { File.Delete(walletFile); }
                catch (Exception ex) { WriteNodeLog("Could not remove the wallet password file: " + ex.Message); }
            }
            _startingNode = false;
        }
        UpdateStatus();
        BuildPage();
        if (retryWalletPassword && !IsDisposed)
            await StartNodeAsync(offerWalletCreation);
    }

    private async Task StopNodeAsync()
    {
        if (!IsNodeProcessRunning()) return;
        _nodeStatus = "Останавливаю ноду…";
        UpdateStatus();
        try
        {
            if (_gpuMiningActive || _gpuMinerProcess is not null) await StopGpuMinerAsync();
            if (_cpuMiningActive && _rpc is not null)
            {
                try { await _rpc.CallAsync("setgenerate", new object[] { false }); } catch { }
                _cpuMiningActive = false;
            }
            if (_rpc is not null) await _rpc.CallAsync("stop");
            for (int i = 0; i < 200 && IsNodeProcessRunning(); i++) await Task.Delay(100);
            if (IsNodeProcessRunning())
                throw new TimeoutException("Нода ещё завершает работу. Подождите немного и повторите.");
            await StopGpuMinerAsync();
            CleanupNodeSession();
            _nodeStatus = "Нода остановлена";
            _rpcHealthy = false;
            _balance = 0;
            _height = 0;
            _headers = 0;
            _connections = 0;
        }
        catch (Exception ex)
        {
            _nodeStatus = "Нода продолжает работать";
            ShowLocalizedMessage(ex.Message + "\n\nОставьте GeoCoin-Start открытым и попробуйте остановить ноду ещё раз.",
                "GeoCoin ещё работает", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        UpdateStatus();
    }

    private async Task CreateWalletAsync()
    {
        if (!IsNodeRunning() || _rpc is null) return;
        if (File.Exists(AppPaths.WalletPath))
        {
            ShowLocalizedMessage("В папке GeoCoin уже есть кошелёк. Он не будет перезаписан.",
                "Кошелёк уже существует", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string? password = PasswordDialog.AskToCreate(this, TranslateUiText);
        if (password is null) return;
        try
        {
            JsonElement result = await _rpc.CallAsync("createwallet", new[] { password });
            _lastAddress = result.TryGetProperty("address", out JsonElement address) ? address.GetString() : null;
            ResetWalletHistoryBackfill();
            _receiveMessage.Text = "Кошелёк создан. Сейчас сделайте резервную копию на странице «Обзор».";
            ShowLocalizedMessage("Кошелёк создан и защищён паролем.\n\n" +
                "Сначала сохраните пароль, затем создайте зашифрованную резервную копию. Храните копию отдельно от компьютера.",
                "Кошелёк готов", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshStateAsync();
        }
        catch (Exception ex)
        {
            ShowLocalizedMessage(ex.Message, "Не удалось создать кошелёк", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task BackupWalletAsync()
    {
        if (!IsNodeRunning() || _rpc is null || !File.Exists(AppPaths.WalletPath)) return;
        using var dialog = new SaveFileDialog
        {
            Title = "Куда сохранить резервную копию",
            FileName = "GeoCoin-wallet-backup.dat",
            Filter = "Файл резервной копии (*.dat)|*.dat",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (File.Exists(dialog.FileName))
        {
            ShowLocalizedMessage("Выберите новый файл. Существующую резервную копию приложение не перезаписывает.",
                "Файл уже существует", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            string rpcBackupPath = dialog.FileName + $".rpc-{Guid.NewGuid():N}";
            try
            {
                await _rpc.CallAsync("backupwallet", new[] { rpcBackupPath });
                File.Move(rpcBackupPath, dialog.FileName, overwrite: false);
            }
            catch
            {
                try { if (File.Exists(rpcBackupPath)) File.Delete(rpcBackupPath); } catch { }
                throw;
            }
            WalletBackupCompleted();
            ShowLocalizedMessage("Копия кошелька сохранена. Храните её отдельно от компьютера; для защищённого файла также сохраните пароль.",
                "Копия готова", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            if (!ex.Message.Contains("encrypted key vault", StringComparison.OrdinalIgnoreCase))
            {
                ShowLocalizedMessage(ex.Message, "Не удалось создать копию", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_cpuMiningActive || _gpuMiningActive || _gpuMinerProcess is { HasExited: false })
            {
                ShowLocalizedMessage("Этот формат кошелька нужно копировать при остановленной ноде. Сначала остановите майнинг, затем повторите резервное копирование.",
                    "Остановите майнинг", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (ShowLocalizedMessage("Этот файл кошелька нельзя скопировать через RPC. GeoCoin-Start может безопасно остановить ноду, скопировать файл и затем запустить ноду снова. При запуске может потребоваться пароль кошелька. Продолжить?",
                    "Создать резервную копию", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            bool restartNode = IsNodeRunning();
            try
            {
                if (restartNode)
                {
                    await StopNodeAsync();
                    if (IsNodeRunning()) throw new InvalidOperationException("Не удалось остановить ноду для создания копии.");
                }
                try
                {
                    File.Copy(AppPaths.WalletPath, dialog.FileName, overwrite: false);
                }
                catch
                {
                    if (restartNode) await StartNodeAsync(offerWalletCreation: false);
                    throw;
                }
                WalletBackupCompleted();
                if (restartNode) await StartNodeAsync(offerWalletCreation: false);
                string restartNote = restartNode && !IsNodeRunning()
                    ? "\n\nКопия создана, но нода осталась остановлена. Запустите её снова со страницы «Нода»."
                    : "";
                ShowLocalizedMessage("Копия кошелька сохранена. Храните её отдельно от компьютера; для защищённого файла также сохраните пароль." + restartNote,
                    "Копия готова", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception fallbackError)
            {
                ShowLocalizedMessage("Не удалось безопасно создать копию кошелька: " + fallbackError.Message,
                    "Не удалось создать копию", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private void WalletBackupCompleted()
    {
        if (_currentPage != "guide" || _firstRunBackupConfirmed) return;
        _firstRunBackupConfirmed = true;
        SaveUiPreferences();
        BuildPage();
    }

    private async Task ImportWalletAsync()
    {
        if (!IsNodeRunning() || _rpc is null) return;
        using var dialog = new OpenFileDialog
        {
            Title = "Выберите файл кошелька GeoCoin",
            Filter = "Файлы GeoCoin (*.dat;*.wallet;*.bin)|*.dat;*.wallet;*.bin|Все файлы (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (ShowLocalizedMessage("Перед импортом закройте оригинальный клиент GeoCoin, если выбранный кошелёк открыт в нём. Исходный файл останется без изменений.",
                "Импорт кошелька", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK)
            return;

        string sourcePath = Path.GetFullPath(dialog.FileName);
        if (string.Equals(sourcePath, Path.GetFullPath(AppPaths.WalletPath), StringComparison.OrdinalIgnoreCase))
        {
            ShowLocalizedMessage("Этот файл уже выбран как текущий кошелёк.", "Кошелёк уже открыт",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string? sourcePassword = PasswordDialog.AskToUnlock(this, TranslateUiText);
        if (sourcePassword is null) return;

        bool replacingWallet = File.Exists(AppPaths.WalletPath);
        string? preservedWallet = null;
        string? recoveryCopy = null;
        bool stoppedForImport = false;
        bool imported = false;
        if (replacingWallet && ShowLocalizedMessage("Текущий кошелёк будет заменён. Перед этим GeoCoin-Start создаст его резервную копию и сохранит исходный файл отдельно. Продолжить?",
                "Импортировать другой кошелёк", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        try
        {
            Directory.CreateDirectory(AppPaths.WalletBackupDirectory);
            if (replacingWallet)
            {
                recoveryCopy = UniqueWalletBackupPath("до-импорта");
                await _rpc.CallAsync("backupwallet", new[] { recoveryCopy });
            }

            await StopNodeAsync();
            if (IsNodeRunning()) throw new InvalidOperationException("Не удалось безопасно остановить ноду GeoCoin.");
            stoppedForImport = true;
            if (replacingWallet)
            {
                preservedWallet = UniqueWalletBackupPath("старый-кошелёк");
                File.Move(AppPaths.WalletPath, preservedWallet);
            }

            // Open the selected wallet file with the same passphrase-based loader used by the official client.
            File.Copy(sourcePath, AppPaths.WalletPath, overwrite: false);
            await StartNodeAsync(offerWalletCreation: false, suppliedWalletPassword: sourcePassword);
            if (!IsNodeRunning()) throw new InvalidOperationException("GeoCoin не смог открыть этот файл. Проверьте пароль и выберите файл кошелька из оригинального клиента.");
            imported = true;
            ResetWalletHistoryBackfill();
            await RefreshStateAsync();
            ShowLocalizedMessage("Кошелёк импортирован и открыт. Исходный файл не изменён. История операций будет синхронизирована с сетью.",
                "Кошелёк импортирован", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            string? failedImport = null;
            if (stoppedForImport && !imported)
            {
                try
                {
                    if (IsNodeRunning()) await StopNodeAsync();
                    if (File.Exists(AppPaths.WalletPath))
                    {
                        failedImport = UniqueWalletBackupPath("неудачный-импорт");
                        File.Move(AppPaths.WalletPath, failedImport);
                    }
                    if (!string.IsNullOrEmpty(preservedWallet) && File.Exists(preservedWallet))
                        File.Move(preservedWallet, AppPaths.WalletPath);
                    await StartNodeAsync(offerWalletCreation: false);
                }
                catch (Exception rollbackError)
                {
                    ShowLocalizedMessage("Импорт не завершился. Исходный кошелёк сохранён здесь:\n" + (preservedWallet ?? recoveryCopy ?? "") +
                        "\n\n" + rollbackError.Message,
                        "Проверьте файл кошелька", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            ShowLocalizedMessage("Не удалось импортировать файл кошелька. Убедитесь, что выбран файл GeoCoin и введён его пароль.\n\n" + ex.Message +
                (recoveryCopy is null ? "" : "\n\nРезервная копия прежнего кошелька сохранена: " + recoveryCopy) +
                (failedImport is null ? "" : "\n\nНеудачная копия сохранена отдельно: " + failedImport),
                "Ошибка импорта", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string UniqueWalletBackupPath(string reason)
        => Path.Combine(AppPaths.WalletBackupDirectory,
            $"wallet-{reason}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.dat");

    private async Task RefreshGpuDevicesAsync()
    {
        if (_checkingGpuDevices || _gpuMiningActive) return;
        _checkingGpuDevices = true;
        if (_currentPage == "mining" || _currentPage == "subminer") BuildPage();
        if (!File.Exists(AppPaths.GpuMinerPath))
        {
            _gpuStatusText = "GPU-майнер не найден. Переустановите GeoCoin-Start.";
            _gpuDevicesScanned = true;
            _checkingGpuDevices = false;
            if (_currentPage == "mining" || _currentPage == "subminer") BuildPage();
            return;
        }

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = NodeCompatiblePath(AppPaths.GpuMinerPath),
                WorkingDirectory = NodeCompatiblePath(AppPaths.OfficialDirectory),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("--list-devices");
            start.ArgumentList.Add("--stats-json");
            using var process = new Process { StartInfo = start };
            if (!process.Start()) throw new InvalidOperationException("Windows не запустил проверку видеокарт.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
            catch
            {
                try { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } catch { }
                throw;
            }
            string output = await stdout;
            string error = await stderr;
            if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Не удалось проверить видеокарты." : error.Trim());

            _gpuDevices.Clear();
            foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(line);
                    JsonElement root = document.RootElement;
                    if (!root.TryGetProperty("devices", out JsonElement devices) || devices.ValueKind != JsonValueKind.Array) continue;
                    foreach (JsonElement device in devices.EnumerateArray())
                    {
                        string backend = device.TryGetProperty("backend", out JsonElement backendValue) ? backendValue.GetString() ?? "" : "";
                        string id = device.TryGetProperty("id", out JsonElement idValue)
                            ? idValue.ValueKind == JsonValueKind.String ? idValue.GetString() ?? "0" : idValue.ToString()
                            : "0";
                        string name = device.TryGetProperty("name", out JsonElement nameValue) ? nameValue.GetString() ?? "Видеокарта" : "Видеокарта";
                        if (!string.IsNullOrWhiteSpace(backend)) _gpuDevices.Add(new GpuDevice(backend, id, name));
                    }
                }
                catch (JsonException) { }
            }
            _gpuDevices.Sort((left, right) => string.Compare(left.ToString(), right.ToString(), StringComparison.CurrentCultureIgnoreCase));
            _selectedGpuDeviceKeys.RemoveWhere(key => !_gpuDevices.Any(device => string.Equals(GpuDeviceKey(device), key, StringComparison.OrdinalIgnoreCase)));
            string[] backends = AvailableGpuBackends();
            if (_selectedGpuBackend is null || !backends.Contains(_selectedGpuBackend, StringComparer.OrdinalIgnoreCase))
                _selectedGpuBackend = backends.FirstOrDefault();
            _initializedGpuBackends.IntersectWith(backends);
            _gpuDevicesScanned = true;
            _gpuStatusText = _gpuDevices.Count == 0
                ? "Совместимая видеокарта не найдена. Проверьте драйверы."
                : $"Найдено видеокарт: {_gpuDevices.Count}.";
        }
        catch (Exception ex)
        {
            _gpuDevicesScanned = true;
            _gpuStatusText = "Не удалось проверить видеокарту.";
            if (_currentPage == "mining" || _currentPage == "subminer")
                ShowLocalizedMessage(ex.Message, "Проверка GPU", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            _checkingGpuDevices = false;
            ReleaseNodePathAliasIfIdle();
            if (_currentPage == "mining" || _currentPage == "subminer") BuildPage();
        }
    }

    private static string GpuDeviceKey(GpuDevice device) => device.Backend + "\0" + device.Id;

    private string[] AvailableGpuBackends()
        => _gpuDevices.Select(device => device.Backend)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(backend => backend.ToLowerInvariant() switch { "cuda" => 0, "opencl" => 1, "amd" => 2, _ => 3 })
            .ThenBy(backend => backend, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private List<GpuDevice> SelectedGpuDevices()
        => _selectedGpuBackend is null
            ? new List<GpuDevice>()
            : _gpuDevices.Where(device =>
                string.Equals(device.Backend, _selectedGpuBackend, StringComparison.OrdinalIgnoreCase) &&
                _selectedGpuDeviceKeys.Contains(GpuDeviceKey(device))).ToList();

    private void ConfigureGpuBackendCombo(ComboBox combo, Point location, Size size, bool enabled)
    {
        _updatingGpuDeviceLists = true;
        try
        {
            string[] backends = AvailableGpuBackends();
            if (_selectedGpuBackend is null || !backends.Contains(_selectedGpuBackend, StringComparer.OrdinalIgnoreCase))
                _selectedGpuBackend = backends.FirstOrDefault();

            combo.BeginUpdate();
            combo.Items.Clear();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (string backend in backends) combo.Items.Add(backend.ToUpperInvariant());
            combo.SelectedIndex = Array.FindIndex(backends, backend =>
                string.Equals(backend, _selectedGpuBackend, StringComparison.OrdinalIgnoreCase));
            combo.DrawMode = DrawMode.Normal;
            combo.FlatStyle = FlatStyle.Flat;
            combo.Location = UiPoint(location.X, location.Y);
            combo.Size = UiSize(size.Width, size.Height);
            combo.Font = new Font("Segoe UI", 10);
            combo.BackColor = InputSurface;
            combo.ForeColor = Ink;
            combo.Enabled = enabled && backends.Length > 0;
        }
        finally
        {
            combo.EndUpdate();
            _updatingGpuDeviceLists = false;
        }
    }

    private void ConfigureGpuDeviceList(CheckedListBox list, Point location, Size size, bool enabled)
    {
        list.Location = UiPoint(location.X, location.Y);
        list.Size = UiSize(size.Width, size.Height);
        list.Font = new Font("Segoe UI", 9.5f);
        list.BackColor = InputSurface;
        list.ForeColor = Ink;
        list.BorderStyle = BorderStyle.FixedSingle;
        list.CheckOnClick = true;
        list.IntegralHeight = false;
        list.HorizontalScrollbar = true;
        list.ItemHeight = ScaleUi(24);
        PopulateGpuDeviceItems(list, enabled);
    }

    private void PopulateGpuDeviceItems(CheckedListBox list, bool enabled)
    {
        _updatingGpuDeviceLists = true;
        try
        {
            list.BeginUpdate();
            list.Items.Clear();
            var devices = _gpuDevices.Where(device =>
                string.Equals(device.Backend, _selectedGpuBackend, StringComparison.OrdinalIgnoreCase)).ToList();
            if (devices.Count > 0 && _selectedGpuBackend is not null && _initializedGpuBackends.Add(_selectedGpuBackend))
                _selectedGpuDeviceKeys.Add(GpuDeviceKey(devices[0]));
            foreach (GpuDevice device in devices)
            {
                list.Items.Add(device, _selectedGpuDeviceKeys.Contains(GpuDeviceKey(device)));
            }
            list.Enabled = enabled && list.Items.Count > 0;
        }
        finally
        {
            list.EndUpdate();
            _updatingGpuDeviceLists = false;
        }
    }

    private void GpuBackendChanged(ComboBox combo, CheckedListBox list)
    {
        if (_updatingGpuDeviceLists || combo.SelectedIndex < 0 ||
            combo.Items[combo.SelectedIndex] is not string backend ||
            string.Equals(backend, _selectedGpuBackend, StringComparison.OrdinalIgnoreCase)) return;
        _selectedGpuBackend = backend.ToLowerInvariant();
        PopulateGpuDeviceItems(list, combo.Enabled);
        UpdateGpuSelectionButtons();
    }

    private void GpuDeviceItemCheck(CheckedListBox list, ItemCheckEventArgs e)
    {
        if (_updatingGpuDeviceLists || e.Index < 0 || e.Index >= list.Items.Count || list.Items[e.Index] is not GpuDevice device) return;
        string key = GpuDeviceKey(device);
        if (e.NewValue == CheckState.Checked) _selectedGpuDeviceKeys.Add(key);
        else _selectedGpuDeviceKeys.Remove(key);
        if (IsHandleCreated)
        {
            BeginInvoke((Action)UpdateGpuSelectionButtons);
        }
    }

    private void UpdateGpuSelectionButtons()
    {
        int selectedCount = SelectedGpuDevices().Count;
        if (_gpuMineButton.IsHandleCreated && !_gpuMiningActive)
            _gpuMineButton.Enabled = _remoteGpuMining || (selectedCount > 0 && IsNodeRunning() && File.Exists(AppPaths.WalletPath));
        if (_remoteGpuButton.IsHandleCreated && !_remoteGpuMining && !_startingRemoteGpu)
            _remoteGpuButton.Enabled = selectedCount > 0 && !_checkingGpuDevices && !IsNodeRunning() && _gpuMinerProcess is null;
    }

    private DialogResult ShowMiningConfirmation(bool gpu, int threads)
    {
        string title;
        string question;
        string explanation;
        if (gpu)
        {
            (title, question, explanation) = _uiLanguage switch
            {
                "en" => ("Start GPU mining", "Start GPU mining on all selected graphics cards?",
                    "The GPUs will run under heavy load and may heat up. Rewards will go to an existing address in the current wallet."),
                "zh-CN" => ("开始 GPU 挖矿", "在所有选中的显卡上开始 GPU 挖矿？",
                    "显卡将以高负载运行，可能会发热。奖励将发送到当前钱包中的现有地址。"),
                "pt-BR" => ("Iniciar mineração de GPU", "Iniciar a mineração por GPU em todas as placas de vídeo selecionadas?",
                    "As GPUs funcionarão sob alta carga e poderão aquecer. As recompensas serão enviadas para um endereço existente na carteira atual."),
                "it" => ("Avvia mining GPU", "Avviare il mining GPU su tutte le schede video selezionate?",
                    "Le GPU lavoreranno sotto carico elevato e potrebbero surriscaldarsi. Le ricompense saranno inviate a un indirizzo esistente del portafoglio corrente."),
                _ => ("Начать GPU-майнинг", "Запустить GPU-майнинг на всех отмеченных видеокартах?",
                    "Видеокарта будет работать с высокой нагрузкой и может нагреваться. Награда поступит на существующий адрес текущего кошелька.")
            };
        }
        else
        {
            string russianThreadWord = RussianThreadWord(threads);
            string englishThreadWord = threads == 1 ? "thread" : "threads";
            string portugueseThreadWord = threads == 1 ? "thread" : "threads";
            (title, question, explanation) = _uiLanguage switch
            {
                "en" => ("Start mining", $"Start CPU mining with {threads} processor {englishThreadWord}?",
                    "This will increase system load, heat, and power use. If GPU mining is already running, both will run at the same time."),
                "zh-CN" => ("开始 CPU 挖矿", $"使用 {threads} 个处理器线程开始 CPU 挖矿？",
                    "这会增加系统负载、发热和耗电。如果 GPU 挖矿已在运行，两者会同时运行。"),
                "pt-BR" => ("Iniciar mineração de CPU", $"Iniciar a mineração de CPU usando {threads} {portugueseThreadWord} do processador?",
                    "Isso aumentará o uso do sistema, o aquecimento e o consumo de energia. Se a mineração por GPU já estiver em execução, ambas funcionarão ao mesmo tempo."),
                "it" => ("Avvia mining CPU", $"Avviare il mining CPU usando {threads} thread del processore?",
                    "Questo aumenterà il carico sul sistema, il calore e il consumo elettrico. Se il mining GPU è già attivo, entrambi funzioneranno contemporaneamente."),
                _ => ("Начать майнинг", $"Запустить майнинг на {threads} {russianThreadWord} процессора?",
                    "Это увеличит нагрузку, нагрев и расход электричества. Если GPU-майнинг уже запущен, обе нагрузки будут работать одновременно.")
            };
        }

        return ThemedMessageDialog.Show(this, question + "\n\n" + explanation, title,
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1,
            _darkTheme, TranslateUiText);
    }

    private static string RussianThreadWord(int count)
    {
        int absolute = Math.Abs(count);
        int lastTwo = absolute % 100;
        if (lastTwo is >= 11 and <= 14) return "потоков";
        return (absolute % 10) switch
        {
            1 => "поток",
            2 or 3 or 4 => "потока",
            _ => "потоков"
        };
    }

    private async Task ToggleCpuMiningAsync()
    {
        if (!IsNodeRunning() || _rpc is null) return;
        if (_cpuMiningActive)
        {
            try
            {
                await _rpc.CallAsync("setgenerate", new object[] { false });
                _cpuMiningActive = false;
                _cpuHashrateValue = 0;
                UpdateMiningSessionState();
                BuildPage();
            }
            catch (Exception ex)
            {
                ShowLocalizedMessage(ex.Message, "Не удалось остановить CPU-майнинг", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }

        if (ShowMiningConfirmation(gpu: false, threads: (int)_threadCount.Value) != DialogResult.Yes)
            return;
        try
        {
            await _rpc.CallAsync("setgenerate", new object[] { true, (int)_threadCount.Value });
            _cpuMiningActive = true;
            UpdateMiningSessionState();
            BuildPage();
        }
        catch (Exception ex)
        {
            ShowLocalizedMessage(ex.Message, "Не удалось запустить CPU-майнинг", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ToggleGpuMiningAsync()
    {
        if (_remoteGpuMining)
        {
            SetPage("subminer");
            return;
        }
        if (_gpuMiningActive || _gpuMinerProcess is not null)
        {
            await StopGpuMinerAsync();
            BuildPage();
            return;
        }
        await StartGpuMiningAsync();
    }

    private async Task StartGpuMiningAsync()
    {
        if (_remoteGpuMining)
        {
            ShowLocalizedMessage("Сейчас GPU-процесс подключён к удалённой ноде. Управляйте им на странице «Подмайнер».",
                "Работает подмайнер", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!IsNodeRunning() || _rpc is null || _minerPassFile is null || !File.Exists(_minerPassFile)) return;
        if (!File.Exists(AppPaths.GpuMinerPath))
        {
            ShowLocalizedMessage("GPU-майнер не найден. Переустановите GeoCoin-Start, чтобы получить официальный пакет целиком.",
                "GPU-майнинг недоступен", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        List<GpuDevice> devices = SelectedGpuDevices();
        if (devices.Count == 0 || _selectedGpuBackend is null)
        {
            ShowLocalizedMessage("Нажмите «Найти» и отметьте одну или несколько видеокарт.",
                "Видеокарта не выбрана", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (ShowMiningConfirmation(gpu: true, threads: 0) != DialogResult.Yes)
            return;

        _gpuLastError = null;
        try
        {
            string address = await GetExistingMiningAddressAsync();

            var start = new ProcessStartInfo
            {
                FileName = NodeCompatiblePath(AppPaths.GpuMinerPath),
                WorkingDirectory = NodeCompatiblePath(AppPaths.OfficialDirectory),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };
            AddArgument(start, "--url", $"http://{_rpcBindAddress}:3033/");
            AddArgument(start, "--rpc-user", _minerRpcUsername);
            AddArgument(start, "--rpc-pass-file", NodeCompatiblePath(_minerPassFile));
            AddArgument(start, "--address", address);
            AddArgument(start, "--backend", _selectedGpuBackend);
            AddArgument(start, "--devices", string.Join(",", devices.Select(device => device.Id)));
            start.ArgumentList.Add("--continuous");
            start.ArgumentList.Add("--control-stdin");
            start.ArgumentList.Add("--stats-json");

            var miner = new Process { StartInfo = start, EnableRaisingEvents = true };
            miner.OutputDataReceived += (_, e) => HandleGpuMinerLine(e.Data, remote: false);
            miner.ErrorDataReceived += (_, e) => HandleGpuMinerErrorLine(e.Data, remote: false);
            miner.Exited += (_, _) =>
            {
                try
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke((Action)(() =>
                    {
                        if (!ReferenceEquals(_gpuMinerProcess, miner)) return;
                        _gpuMinerProcess = null;
                        _gpuMiningActive = false;
                        _remoteGpuMining = false;
                        UpdateMiningSessionState();
                        DeleteRemoteMinerPassFile();
                        int exitCode = 0;
                        try { exitCode = miner.ExitCode; } catch { }
                        _gpuStatusText = DescribeGpuMinerExit(exitCode, remote: false);
                        _gpuHashrateText = "Скорость GPU: —";
                        ReleaseNodePathAliasIfIdle();
                        if (!IsDisposed && (_currentPage == "mining" || _currentPage == "subminer")) BuildPage();
                        if (!IsDisposed && _currentPage == "node") UpdateNodePage();
                    }));
                }
                catch (InvalidOperationException) { }
            };
            if (!miner.Start()) throw new InvalidOperationException("Windows не запустил GPU-майнер.");
            _gpuMinerProcess = miner;
            if (miner.HasExited)
            {
                _gpuMinerProcess = null;
                throw new InvalidOperationException("GPU-майнер завершился сразу после запуска. Проверьте драйвер видеокарты.");
            }
            _gpuMiningActive = true;
            UpdateMiningSessionState();
            miner.BeginOutputReadLine();
            miner.BeginErrorReadLine();
            _lastAddress = address;
            _gpuStatusText = "GPU-майнинг запущен. Ожидаю данные о скорости…";
            _gpuHashrateText = "Скорость GPU: ожидание данных…";
            BuildPage();
        }
        catch (Exception ex)
        {
            _gpuLastError = ex.Message;
            _gpuStatusText = "Не удалось запустить GPU-майнинг: " + ex.Message;
            ReleaseNodePathAliasIfIdle();
            ShowLocalizedMessage(ex.Message, "Не удалось запустить GPU-майнинг", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ToggleRemoteGpuMiningAsync()
    {
        if (_startingRemoteGpu) return;
        if (_remoteGpuMining && _gpuMinerProcess is not null)
        {
            await StopGpuMinerAsync();
            BuildPage();
            return;
        }
        if (_gpuMinerProcess is { HasExited: false })
        {
            ShowLocalizedMessage("GPU-майнер уже запущен. Остановите текущий процесс перед сменой режима.",
                "GPU уже занят", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (IsNodeProcessRunning())
        {
            ShowLocalizedMessage("Сначала остановите локальную ноду. Подмайнер работает отдельно и подключается к ноде на другом ПК.",
                "Остановите локальную ноду", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!File.Exists(AppPaths.GpuMinerPath))
        {
            ShowLocalizedMessage("Официальный GPU-майнер не найден. Переустановите GeoCoin-Start.",
                "GPU-майнинг недоступен", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Uri.TryCreate(_remoteNodeUrlInput.Text.Trim(), UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttp ||
            endpoint.Host != "127.0.0.1" ||
            !string.IsNullOrEmpty(endpoint.UserInfo))
        {
            ShowLocalizedMessage("Введите адрес локального перенаправления защищённого туннеля, например http://127.0.0.1:3033/.",
                "Проверьте адрес ноды", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string username = _remoteMinerUserInput.Text.Trim();
        string password = _remoteMinerPasswordInput.Text;
        string address = _remoteRewardAddressInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(username) || username.Contains(':') || username.Any(char.IsWhiteSpace) || password.Length < 16 || string.IsNullOrWhiteSpace(address))
        {
            ShowLocalizedMessage("Укажите имя miner RPC без пробелов, пароль не короче 16 символов и публичный адрес для награды.",
                "Заполните данные подмайнера", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        List<GpuDevice> devices = SelectedGpuDevices();
        if (devices.Count == 0 || _selectedGpuBackend is null)
        {
            ShowLocalizedMessage("Нажмите «Найти» и отметьте одну или несколько видеокарт.", "Видеокарта не выбрана",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (ShowLocalizedMessage("Подключить выбранные видеокарты к удалённой RPC-ноде?\n\n" +
                "Для удалённой ноды используйте локальный порт защищённого туннеля. Введите только учётные данные miner RPC.",
                "Запустить GPU-подмайнер", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        string? passFile = null;
        _gpuLastError = null;
        _startingRemoteGpu = true;
        _gpuStatusText = "Проверяю miner RPC и адрес награды…";
        BuildPage();
        try
        {
            WriteSubminerRpcLog("Проверяю RPC", $"{endpoint.Host}:{endpoint.Port} · getblocktemplate");
            using (var rpcCheck = new GeoCoinRpcClient(username, password, endpoint.ToString(), subminer: true))
            {
                JsonElement template = await rpcCheck.CallAsync("getblocktemplate", new object[]
                {
                    new Dictionary<string, string> { ["address"] = address }
                });
                if (template.ValueKind != JsonValueKind.Object || !template.TryGetProperty("blockhex", out _))
                    throw new InvalidOperationException("Нода не вернула шаблон блока. Проверьте синхронизацию ноды и miner RPC-доступ.");
            }

            WriteSubminerRpcLog("Нода ответила на проверочный запрос");
            _gpuStatusText = "Подготавливаю подключение к удалённой ноде…";
            string tempDirectory = Path.Combine(AppPaths.NodeSessionDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            passFile = Path.Combine(tempDirectory, "rpc-miner.pass");
            await File.WriteAllTextAsync(passFile, password + Environment.NewLine, new UTF8Encoding(false));
            var start = new ProcessStartInfo
            {
                FileName = NodeCompatiblePath(AppPaths.GpuMinerPath),
                WorkingDirectory = NodeCompatiblePath(AppPaths.OfficialDirectory),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };
            AddArgument(start, "--url", endpoint.ToString());
            AddArgument(start, "--rpc-user", username);
            AddArgument(start, "--rpc-pass-file", NodeCompatiblePath(passFile));
            AddArgument(start, "--address", address);
            AddArgument(start, "--backend", _selectedGpuBackend);
            AddArgument(start, "--devices", string.Join(",", devices.Select(device => device.Id)));
            start.ArgumentList.Add("--continuous");
            start.ArgumentList.Add("--control-stdin");
            start.ArgumentList.Add("--stats-json");

            var miner = new Process { StartInfo = start, EnableRaisingEvents = true };
            bool rpcConfirmed = false;
            miner.OutputDataReceived += (_, e) => HandleGpuMinerLine(e.Data, remote: true, onTemplate: height =>
            {
                if (!IsHandleCreated || IsDisposed) return;
                BeginInvoke((Action)(() =>
                {
                    if (!ReferenceEquals(_gpuMinerProcess, miner) || !_remoteGpuMining) return;
                    WriteSubminerRpcLog("Получен шаблон блока", height > 0 ? height.ToString(CultureInfo.InvariantCulture) : null);
                    if (rpcConfirmed) return;
                    rpcConfirmed = true;
                    ConfirmRpcConnection($"{endpoint.Host}:{endpoint.Port}");
                }));
            });
            miner.ErrorDataReceived += (_, e) => HandleGpuMinerErrorLine(e.Data, remote: true);
            miner.Exited += (_, _) =>
            {
                try
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke((Action)(() =>
                    {
                        if (!ReferenceEquals(_gpuMinerProcess, miner)) return;
                        _gpuMinerProcess = null;
                        _gpuMiningActive = false;
                        _remoteGpuMining = false;
                        UpdateMiningSessionState();
                        DeleteRemoteMinerPassFile();
                        int exitCode = 0;
                        try { exitCode = miner.ExitCode; } catch { }
                        WriteSubminerRpcLog("Подмайнер завершил работу", exitCode.ToString(CultureInfo.InvariantCulture));
                        _gpuStatusText = DescribeGpuMinerExit(exitCode, remote: true);
                        _gpuHashrateText = "Скорость GPU: —";
                        ReleaseNodePathAliasIfIdle();
                        if (!IsDisposed && (_currentPage == "subminer" || _currentPage == "mining")) BuildPage();
                        if (!IsDisposed && _currentPage == "node") UpdateNodePage();
                    }));
                }
                catch (InvalidOperationException) { }
            };
            if (!miner.Start()) throw new InvalidOperationException("Windows не запустил GPU-подмайнер.");
            _gpuMinerProcess = miner;
            _remoteMinerPassFile = passFile;
            passFile = null;
            if (miner.HasExited)
            {
                _gpuMinerProcess = null;
                DeleteRemoteMinerPassFile();
                _remoteGpuMining = false;
                throw new InvalidOperationException("GPU-подмайнер сразу завершился. Проверьте адрес, miner RPC-пароль и доступность видеокарты.");
            }
            _remoteGpuMining = true;
            _gpuMiningActive = true;
            UpdateMiningSessionState();
            miner.BeginOutputReadLine();
            miner.BeginErrorReadLine();
            WriteNodeLog($"[GPU-подмайнер] Подключение к {endpoint.Host}:{endpoint.Port}, устройства {string.Join(", ", devices.Select(selectedDevice => selectedDevice.Name))}, адрес награды {address}.");
            WriteSubminerRpcLog("Ожидаю первый ответ RPC подмайнера…");
            _gpuStatusText = "Ожидаю первый ответ RPC подмайнера…";
            _gpuHashrateText = "Скорость GPU: ожидание данных…";
            BuildPage();
        }
        catch (Exception ex)
        {
            _gpuLastError = ex.Message;
            _gpuStatusText = "Не удалось подключиться: " + ex.Message;
            WriteSubminerRpcLog("Не удалось запустить подмайнер", ex switch
            {
                GeoCoinRpcException rpcError when rpcError.Code is int code => $"JSON-RPC {code}",
                HttpRequestException httpError when httpError.StatusCode is { } status => $"HTTP {(int)status} · {TranslateUiText(httpError.Message)}",
                HttpRequestException => TranslateUiText("Нет HTTP-ответа от RPC. Проверьте туннель и запустите ноду."),
                OperationCanceledException => TranslateUiText("Истекло время ожидания RPC."),
                _ => null
            });
            if (passFile is not null)
            {
                try { Directory.Delete(Path.GetDirectoryName(passFile)!, recursive: true); } catch { }
            }
            ShowLocalizedMessage(ex.Message, "Не удалось запустить подмайнер", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _startingRemoteGpu = false;
            ReleaseNodePathAliasIfIdle();
            if (!IsDisposed && (_currentPage == "subminer" || _currentPage == "node")) BuildPage();
        }
    }

    private void DeleteRemoteMinerPassFile()
    {
        string? passFile = _remoteMinerPassFile;
        _remoteMinerPassFile = null;
        if (string.IsNullOrWhiteSpace(passFile)) return;
        try { Directory.Delete(Path.GetDirectoryName(passFile)!, recursive: true); } catch { }
    }

    private async Task<string> GetExistingMiningAddressAsync()
    {
        if (_rpc is null) throw new InvalidOperationException("Нода GeoCoin не подключена.");
        JsonElement result = await _rpc.CallAsync("getaddressesbyaccount", new object[] { "*" });
        if (result.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Нода не вернула список адресов текущего кошелька.");
        string[] addresses = result.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? "")
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (addresses.Length == 0)
            throw new InvalidOperationException("В кошельке пока нет адреса для награды. Сначала создайте адрес на странице «Получить».");
        if (string.IsNullOrWhiteSpace(_lastAddress) || !addresses.Contains(_lastAddress, StringComparer.Ordinal))
            _lastAddress = addresses[0];
        return _lastAddress;
    }

    private void HandleGpuMinerErrorLine(string? line, bool remote)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        string message = line.Trim();
        string prefix = remote ? "[GPU-подмайнер] " : "[GPU] ";
        WriteNodeLog(prefix + message);
        _gpuLastError = message;
        _gpuStatusText = (remote ? "Ошибка GPU-подмайнера: " : "Ошибка GPU: ") + message;
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke((Action)(() =>
            {
                if (_currentPage is "mining" or "subminer") _gpuMiningState.Text = TranslateUiText(_gpuStatusText);
            }));
        }
        catch (InvalidOperationException) { }
    }

    private string DescribeGpuMinerExit(int exitCode, bool remote)
    {
        string name = remote ? "GPU-подмайнер" : "GPU-майнер";
        if (exitCode == 0) return remote ? "GPU-подмайнер остановлен." : "GPU-майнинг остановлен.";
        return string.IsNullOrWhiteSpace(_gpuLastError)
            ? $"{name} завершился с кодом {exitCode}. Откройте вкладку «Нода», чтобы посмотреть журнал."
            : $"{name}: {_gpuLastError} (код {exitCode}). Подробности — во вкладке «Нода».";
    }

    private void HandleGpuMinerLine(string? line, bool remote, Action<long>? onTemplate = null)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            string eventName = root.TryGetProperty("event", out JsonElement eventValue) ? eventValue.GetString() ?? "" : "";
            if (eventName == "template") onTemplate?.Invoke(ReadInt64(root, "height"));
            else if (remote && eventName == "state" && ReadString(root, "name") == "Retrying")
                QueueSubminerRpcLog("Подмайнер повторяет запрос RPC");
            else if (remote && eventName is "block_accepted" or "block_rejected")
                QueueSubminerRpcLog(eventName == "block_accepted" ? "Нода приняла блок" : "Нода отклонила блок");
            if (remote && eventName == "error") QueueSubminerRpcLog("Подмайнер сообщил об ошибке");
            if (eventName == "template") return;
            else if (eventName == "block_accepted")
            {
                string blockId = ReadString(root, "hash");
                if (string.IsNullOrWhiteSpace(blockId)) blockId = ReadString(root, "blockhash");
                if (!string.IsNullOrWhiteSpace(blockId))
                {
                    long height = ReadInt64(root, "height");
                    string source = remote ? "remote-gpu" : "local-gpu";
                    string label = remote ? "GPU-подмайнер" : "GPU";
                    RegisterMinedBlock(new MinedBlockRecord(blockId, "", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), source));
                    WriteNodeLog($"[{label}] Нода приняла блок {height:N0}: {blockId}");
                }
            }
            else if (eventName == "error")
            {
                string message = root.TryGetProperty("message", out JsonElement msg) ? msg.GetString() ?? "Ошибка GPU-майнера" : "Ошибка GPU-майнера";
                HandleGpuMinerErrorLine(message, remote);
            }
            else if (eventName == "status")
            {
                string rate = "";
                if (root.TryGetProperty("hashrate", out JsonElement rateValue))
                {
                    string rawRate = rateValue.ValueKind == JsonValueKind.String ? rateValue.GetString() ?? "" : rateValue.ToString();
                    if (rateValue.ValueKind == JsonValueKind.Number && rateValue.TryGetDouble(out double numericRate))
                        rate = FormatHashrate(numericRate);
                    else if (double.TryParse(rawRate, NumberStyles.Float, CultureInfo.InvariantCulture, out numericRate))
                        rate = FormatHashrate(numericRate);
                    else rate = rawRate;
                }
                string state = root.TryGetProperty("message", out JsonElement stateValue) ? stateValue.GetString() ?? "" : "";
                _gpuStatusText = string.IsNullOrWhiteSpace(state) ? "GPU-майнинг работает" : state;
                _gpuHashrateText = string.IsNullOrWhiteSpace(rate) ? "Скорость GPU: —" : $"Скорость GPU: {rate}";
                if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)(() =>
                {
                    if (!IsDisposed && (_currentPage == "mining" || _currentPage == "subminer"))
                    {
                        _gpuMiningState.Text = TranslateUiText(_gpuStatusText);
                        _miningHashrate.Text = TranslateUiText(_gpuHashrateText);
                    }
                }));
            }
        }
        catch (JsonException) { WriteNodeLog(remote ? "[GPU-подмайнер] " + line : "[GPU] " + line); }
        catch (InvalidOperationException) { }
    }

    private void LoadMinedBlockRecords()
    {
        try
        {
            if (!File.Exists(AppPaths.MinedBlocksPath)) return;
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(AppPaths.MinedBlocksPath, Encoding.UTF8));
            List<MinedBlockRecord>? records;
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                records = JsonSerializer.Deserialize<List<MinedBlockRecord>>(document.RootElement.GetRawText());
            }
            else
            {
                MinedBlockLedger? ledger = JsonSerializer.Deserialize<MinedBlockLedger>(document.RootElement.GetRawText());
                records = ledger?.Blocks;
                if (ledger?.LifetimeBlockIds is not null)
                    foreach (string blockId in ledger.LifetimeBlockIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                        _lifetimeMinedBlockIds.Add(blockId);
                if (ledger?.TransactionToBlockIds is not null)
                    foreach ((string txid, string blockId) in ledger.TransactionToBlockIds)
                        if (!string.IsNullOrWhiteSpace(txid) && !string.IsNullOrWhiteSpace(blockId))
                            _minedBlockIdByTransactionId[txid] = blockId;
                _walletHistoryBackfilled = ledger?.WalletHistoryBackfilled ?? false;
                if (ledger?.SessionStartedAtUnix is long sessionStartedAt && sessionStartedAt > 0)
                    _miningSessionStartedAt = DateTimeOffset.FromUnixTimeSeconds(sessionStartedAt);
                if (ledger?.SessionEndedAtUnix is long sessionEndedAt && sessionEndedAt > 0)
                    _miningSessionEndedAt = DateTimeOffset.FromUnixTimeSeconds(sessionEndedAt);
                else if (_miningSessionStartedAt.HasValue)
                    _miningSessionEndedAt = DateTimeOffset.Now;
                if (ledger?.SessionBlockIds is not null)
                    foreach (string blockId in ledger.SessionBlockIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                        _miningSessionBlockIds.Add(blockId);
            }
            if (records is null) return;
            long keepAfter = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
            foreach (MinedBlockRecord record in records)
            {
                if (string.IsNullOrWhiteSpace(record.BlockId) || record.Timestamp < keepAfter || record.Timestamp <= 0 ||
                    record.Timestamp > DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds())
                    continue;
                if (_minedBlockRecords.All(item => !string.Equals(item.BlockId, record.BlockId, StringComparison.OrdinalIgnoreCase)))
                    _minedBlockRecords.Add(record);
                _lifetimeMinedBlockIds.Add(record.BlockId);
                if (!string.IsNullOrWhiteSpace(record.TransactionId))
                    _minedBlockIdByTransactionId[record.TransactionId] = record.BlockId;
            }
        }
        catch (Exception ex)
        {
            WriteNodeLog("Не удалось загрузить счётчик добытых блоков: " + ex.Message);
        }
    }

    private async Task RefreshWalletMinedBlocksAsync()
    {
        GeoCoinRpcClient? rpc = _rpc;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (rpc is null || !File.Exists(AppPaths.WalletPath) || now - _lastWalletMiningStatsPollUtc < TimeSpan.FromSeconds(20))
            return;

        _lastWalletMiningStatsPollUtc = now;
        try
        {
            JsonElement result = await rpc.CallAsync("listtransactions", new object[] { "*", 1000, 0, true });
            if (!ReferenceEquals(rpc, _rpc) || result.ValueKind != JsonValueKind.Array) return;
            List<JsonElement> entries = result.EnumerateArray().Select(entry => entry.Clone()).ToList();

            lock (_minedBlocksLock)
            {
                foreach (JsonElement entry in entries)
                    UpdateMinedBlockFromWalletEntryLocked(entry);

                PruneMinedBlockRecordsLocked();
                SaveMinedBlockRecordsLocked();
            }
            UpdateIncomingTransferNotifications(entries);
            RefreshMinedBlockCountersOnUi();
        }
        catch
        {
            // The node may be starting, the wallet may be unavailable, or RPC may be reconnecting.
        }
    }

    private void UpdateIncomingTransferNotifications(IEnumerable<JsonElement> entries)
    {
        var currentIncomingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement entry in entries)
        {
            if (!string.Equals(ReadString(entry, "category"), "receive", StringComparison.OrdinalIgnoreCase)) continue;
            string txid = ReadString(entry, "txid");
            if (txid.Length == 64 && txid.All(Uri.IsHexDigit))
                currentIncomingIds.Add(txid);
        }

        if (!_incomingTransferBaselineReady)
        {
            _knownIncomingTransferIds = currentIncomingIds;
            _incomingTransferBaselineReady = true;
            return;
        }

        List<string> newTransfers = currentIncomingIds
            .Where(txid => !_knownIncomingTransferIds.Contains(txid))
            .ToList();
        _knownIncomingTransferIds = currentIncomingIds;

        if (!_notificationsEnabled || !_notifyIncomingTransfers) return;
        foreach (string _ in newTransfers)
            ShowSystemNotification("Новый входящий перевод GeoCoin",
                "Входящий перевод появился в истории кошелька. Откройте историю для подробностей.");
    }

    private async Task RefreshLifetimeWalletMinedBlocksAsync()
    {
        GeoCoinRpcClient? rpc = _rpc;
        if (_walletHistoryBackfilled || rpc is null || !File.Exists(AppPaths.WalletPath)) return;

        try
        {
            JsonElement history = await rpc.CallAsync("listsinceblock", new object[] { "", 1, true, true, true });
            if (!ReferenceEquals(rpc, _rpc) || history.ValueKind != JsonValueKind.Object ||
                !history.TryGetProperty("transactions", out JsonElement transactions) || transactions.ValueKind != JsonValueKind.Array)
                return;

            lock (_minedBlocksLock)
            {
                foreach (JsonElement entry in transactions.EnumerateArray())
                    UpdateMinedBlockFromWalletEntryLocked(entry);
                if (history.TryGetProperty("removed", out JsonElement removed) && removed.ValueKind == JsonValueKind.Array)
                    foreach (JsonElement entry in removed.EnumerateArray())
                        RemoveMinedBlockLocked(ReadString(entry, "blockhash"), ReadString(entry, "txid"));

                _walletHistoryBackfilled = true;
                PruneMinedBlockRecordsLocked();
                SaveMinedBlockRecordsLocked();
            }
            RefreshMinedBlockCountersOnUi();
        }
        catch
        {
            // Keep retrying after the wallet finishes syncing or RPC becomes available.
        }
    }

    private void ResetWalletHistoryBackfill()
    {
        lock (_minedBlocksLock)
        {
            _walletHistoryBackfilled = false;
            SaveMinedBlockRecordsLocked();
        }
        _incomingTransferBaselineReady = false;
        _knownIncomingTransferIds.Clear();
    }

    private void UpdateMinedBlockFromWalletEntryLocked(JsonElement entry)
    {
        string category = ReadString(entry, "category").ToLowerInvariant();
        string txid = ReadString(entry, "txid");
        string blockId = ReadString(entry, "blockhash");
        if (category == "orphan")
        {
            RemoveMinedBlockLocked(blockId, txid);
            return;
        }
        if (category is not ("immature" or "generate") || string.IsNullOrWhiteSpace(txid)) return;

        long timestamp = ReadInt64(entry, "blocktime");
        if (timestamp <= 0) timestamp = ReadInt64(entry, "time");
        if (timestamp <= 0 || timestamp > DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()) return;
        if (string.IsNullOrWhiteSpace(blockId)) blockId = txid;
        UpsertMinedBlockLocked(new MinedBlockRecord(blockId, txid, timestamp, "local-wallet"));
    }

    private void RegisterMinedBlock(MinedBlockRecord record)
    {
        lock (_minedBlocksLock)
        {
            UpsertMinedBlockLocked(record);
            PruneMinedBlockRecordsLocked();
            SaveMinedBlockRecordsLocked();
        }
        RefreshMinedBlockCountersOnUi();
    }

    private void UpsertMinedBlockLocked(MinedBlockRecord record)
    {
        _lifetimeMinedBlockIds.Add(record.BlockId);
        if (!string.IsNullOrWhiteSpace(record.TransactionId))
            _minedBlockIdByTransactionId[record.TransactionId] = record.BlockId;
        int existingIndex = _minedBlockRecords.FindIndex(item =>
            string.Equals(item.BlockId, record.BlockId, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            MinedBlockRecord existing = _minedBlockRecords[existingIndex];
            bool hasGpuAcceptanceTime = existing.Source is "local-gpu" or "remote-gpu";
            record = record with
            {
                TransactionId = string.IsNullOrWhiteSpace(record.TransactionId) ? existing.TransactionId : record.TransactionId,
                Timestamp = hasGpuAcceptanceTime ? existing.Timestamp : record.Timestamp,
                Source = hasGpuAcceptanceTime ? existing.Source : record.Source
            };
            _minedBlockRecords[existingIndex] = record;
        }
        else
        {
            _minedBlockRecords.Add(record);
        }

        long timestamp = record.Timestamp;
        bool belongsToSession = _miningSessionStartedAt is DateTimeOffset sessionStart &&
            timestamp >= sessionStart.ToUnixTimeSeconds() &&
            (_miningSessionActive || (_miningSessionEndedAt is DateTimeOffset sessionEnd && timestamp <= sessionEnd.ToUnixTimeSeconds()));
        if (belongsToSession)
            _miningSessionBlockIds.Add(record.BlockId);
        else if (existingIndex >= 0)
            _miningSessionBlockIds.Remove(record.BlockId);
    }

    private void RemoveMinedBlockLocked(string blockId, string transactionId)
    {
        var blockIdsToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(blockId)) blockIdsToRemove.Add(blockId);
        if (!string.IsNullOrWhiteSpace(transactionId) &&
            _minedBlockIdByTransactionId.TryGetValue(transactionId, out string? mappedBlockId))
            blockIdsToRemove.Add(mappedBlockId);
        foreach (MinedBlockRecord record in _minedBlockRecords.Where(record =>
                     (!string.IsNullOrWhiteSpace(blockId) && string.Equals(record.BlockId, blockId, StringComparison.OrdinalIgnoreCase)) ||
                     (!string.IsNullOrWhiteSpace(transactionId) && string.Equals(record.TransactionId, transactionId, StringComparison.OrdinalIgnoreCase))).ToArray())
        {
            _minedBlockRecords.Remove(record);
            _miningSessionBlockIds.Remove(record.BlockId);
            blockIdsToRemove.Add(record.BlockId);
        }
        foreach (string id in blockIdsToRemove) _lifetimeMinedBlockIds.Remove(id);
        if (!string.IsNullOrWhiteSpace(transactionId)) _minedBlockIdByTransactionId.Remove(transactionId);
    }

    private void UpdateMiningSessionState()
    {
        bool changed = false;
        lock (_minedBlocksLock)
        {
            bool miningActive = _cpuMiningActive || _gpuMiningActive;
            if (miningActive && !_miningSessionActive)
            {
                _miningSessionStartedAt = DateTimeOffset.Now;
                _miningSessionEndedAt = null;
                _miningSessionBlockIds.Clear();
                _miningSessionActive = true;
                changed = true;
            }
            else if (!miningActive && _miningSessionActive)
            {
                _miningSessionActive = false;
                _miningSessionEndedAt = DateTimeOffset.Now;
                changed = true;
            }
            if (changed)
            {
                PruneMinedBlockRecordsLocked();
                SaveMinedBlockRecordsLocked();
            }
        }
        RefreshMinedBlockCountersOnUi();
    }

    private void PruneMinedBlockRecordsLocked()
    {
        long keepAfter = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
        _minedBlockRecords.RemoveAll(record => record.Timestamp < keepAfter &&
            (!_miningSessionActive || !_miningSessionBlockIds.Contains(record.BlockId)));
    }

    private void SaveMinedBlockRecordsLocked()
    {
        string? tempPath = null;
        try
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            tempPath = AppPaths.MinedBlocksPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var ledger = new MinedBlockLedger
            {
                Blocks = _minedBlockRecords.ToList(),
                SessionStartedAtUnix = _miningSessionStartedAt?.ToUnixTimeSeconds(),
                SessionEndedAtUnix = _miningSessionEndedAt?.ToUnixTimeSeconds(),
                SessionBlockIds = _miningSessionBlockIds.ToList(),
                LifetimeBlockIds = _lifetimeMinedBlockIds.ToList(),
                TransactionToBlockIds = new Dictionary<string, string>(_minedBlockIdByTransactionId, StringComparer.OrdinalIgnoreCase),
                WalletHistoryBackfilled = _walletHistoryBackfilled
            };
            File.WriteAllText(tempPath, JsonSerializer.Serialize(ledger), new UTF8Encoding(false));
            File.Move(tempPath, AppPaths.MinedBlocksPath, overwrite: true);
        }
        catch (Exception ex)
        {
            WriteNodeLog("Не удалось сохранить счётчик добытых блоков: " + ex.Message);
            if (tempPath is not null)
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    private void RefreshMinedBlockCountersOnUi()
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke((Action)(() =>
            {
                UpdateMinedBlockCounters();
                if (_currentPage == "overview") UpdateOverviewMiningSummary();
            })); } catch (InvalidOperationException) { }
            return;
        }
        UpdateMinedBlockCounters();
        if (_currentPage == "overview") UpdateOverviewMiningSummary();
    }

    private void UpdateMinedBlockCounters()
    {
        if (_minedBlocksTodayLabel.IsDisposed || _minedBlocksSessionLabel.IsDisposed) return;
        int todayCount;
        int sessionCount;
        bool sessionActive;
        bool sessionStarted;
        DateTime today = DateTime.Now.Date;
        lock (_minedBlocksLock)
        {
            todayCount = _minedBlockRecords
                .Where(record => record.Timestamp > 0 && DateTimeOffset.FromUnixTimeSeconds(record.Timestamp).ToLocalTime().Date == today)
                .Select(record => record.BlockId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            sessionCount = _miningSessionBlockIds.Count;
            sessionActive = _miningSessionActive;
            sessionStarted = _miningSessionStartedAt.HasValue;
        }

        _minedBlocksTodayLabel.Text = $"Сегодня: {FormatBlockCount(todayCount)}";
        string sessionTitle = sessionActive ? "Текущая сессия" : sessionStarted ? "Последняя сессия" : "Сессия";
        _minedBlocksSessionLabel.Text = $"{sessionTitle}: {FormatBlockCount(sessionCount)}";
    }

    private static string FormatBlockCount(int count)
    {
        long value = Math.Abs((long)count);
        long lastTwoDigits = value % 100;
        long lastDigit = value % 10;
        string noun = lastTwoDigits is >= 11 and <= 14 ? "блоков" : lastDigit switch
        {
            1 => "блок",
            >= 2 and <= 4 => "блока",
            _ => "блоков"
        };
        return $"{count:N0} {noun}";
    }

    private async Task StopGpuMinerAsync()
    {
        Process? miner = _gpuMinerProcess;
        if (miner is null)
        {
            _gpuMiningActive = false;
            _remoteGpuMining = false;
            UpdateMiningSessionState();
            DeleteRemoteMinerPassFile();
            ReleaseNodePathAliasIfIdle();
            return;
        }
        try
        {
            if (!miner.HasExited)
            {
                try
                {
                    await miner.StandardInput.WriteLineAsync("stop");
                    await miner.StandardInput.FlushAsync();
                    await miner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                }
                catch (TimeoutException) { miner.Kill(entireProcessTree: true); await miner.WaitForExitAsync(); }
                catch (InvalidOperationException) { }
            }
        }
        finally
        {
            if (ReferenceEquals(_gpuMinerProcess, miner)) _gpuMinerProcess = null;
            try { miner.Dispose(); } catch { }
            _gpuMiningActive = false;
            _remoteGpuMining = false;
            UpdateMiningSessionState();
            DeleteRemoteMinerPassFile();
            _gpuStatusText = "GPU-майнинг остановлен";
            _gpuHashrateText = "Скорость GPU: —";
            ReleaseNodePathAliasIfIdle();
        }
    }

    private async Task CreateReceiveAddressAsync()
    {
        if (!IsNodeRunning() || _rpc is null) return;
        string? password = PasswordDialog.AskForAction(this, "создать адрес для получения", TranslateUiText);
        if (password is null) return;
        try
        {
            var parameters = new object[] { "Получение", password };
            JsonElement result = await _rpc.CallAsync("getnewaddress", parameters);
            _lastAddress = result.ValueKind == JsonValueKind.String ? result.GetString() : result.ToString();
            await RefreshReceiveAddressesAsync(_lastAddress);
            _receiveMessage.Text = "Адрес создан. Его можно скопировать или отсканировать QR-код.";
            UpdateReceiveAddressDisplay();
            BuildPage();
        }
        catch (Exception ex)
        {
            ShowLocalizedMessage(ex.Message, "Не удалось создать адрес", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CopyReceiveAddress()
    {
        string? address = _receiveAddressCombo.SelectedItem as string ?? _lastAddress;
        if (string.IsNullOrWhiteSpace(address))
        {
            ShowLocalizedMessage("В текущем кошельке пока нет адресов. Создайте новый адрес.", "Адрес ещё не создан",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _lastAddress = address;
        Clipboard.SetText(address);
        _receiveMessage.Text = "Адрес скопирован. Теперь его можно отправить отправителю.";
    }

    private void SaveReceiveQrCode()
    {
        if (_receiveQrCode.Image is not Image qrImage || _receiveAddressCombo.SelectedItem is not string) return;
        using var dialog = new SaveFileDialog
        {
            Title = TranslateUiText("Сохранить QR-код адреса"),
            FileName = "GeoCoin-address.png",
            Filter = "Изображение PNG (*.png)|*.png",
            AddExtension = true,
            DefaultExt = "png",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            qrImage.Save(dialog.FileName, ImageFormat.Png);
            _receiveMessage.Text = "QR-код сохранён как изображение PNG.";
            _receiveMessage.ForeColor = Success;
            ApplyUiLanguage(_receiveMessage);
        }
        catch (Exception ex)
        {
            _receiveMessage.Text = "Не удалось сохранить QR-код: " + ex.Message;
            _receiveMessage.ForeColor = Error;
            ApplyUiLanguage(_receiveMessage);
        }
    }

    private async void ImportPaymentQrFromImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = TranslateUiText("Выберите изображение с QR-кодом GeoCoin"),
            Filter = _uiLanguage switch
            {
                "en" => "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
                "zh-CN" => "图像 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件 (*.*)|*.*",
                "pt-BR" => "Imagens (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos os arquivos (*.*)|*.*",
                "it" => "Immagini (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Tutti i file (*.*)|*.*",
                _ => "Изображения (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Все файлы (*.*)|*.*"
            },
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            string payload;
            using (Image source = Image.FromFile(dialog.FileName))
            using (var bitmap = new Bitmap(source))
            {
                var reader = new BarcodeReader
                {
                    AutoRotate = true,
                    Options = new DecodingOptions { TryHarder = true }
                };
                payload = reader.Decode(bitmap)?.Text?.Trim() ?? "";
            }
            if (payload.Length == 0)
                throw new InvalidOperationException("В выбранном изображении не найден QR-код.");

            string address = payload;
            string? amountText = null;
            if (payload.StartsWith("geocoin:", StringComparison.OrdinalIgnoreCase))
            {
                string body = payload["geocoin:".Length..].TrimStart('/');
                int queryIndex = body.IndexOf('?');
                if (queryIndex >= 0)
                {
                    string query = body[(queryIndex + 1)..];
                    body = body[..queryIndex];
                    foreach (string parameter in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] pair = parameter.Split('=', 2);
                        if (pair.Length == 2 && Uri.UnescapeDataString(pair[0]).Equals("amount", StringComparison.OrdinalIgnoreCase))
                            amountText = Uri.UnescapeDataString(pair[1]);
                    }
                }
                address = Uri.UnescapeDataString(body.TrimEnd('/'));
            }
            if (address.Length < 20 || address.Any(char.IsWhiteSpace))
                throw new InvalidOperationException("QR-код не содержит корректный адрес GeoCoin.");

            if (_rpc is not null && IsNodeRunning())
            {
                JsonElement validation = await _rpc.CallAsync("validateaddress", new object[] { address });
                if (validation.ValueKind != JsonValueKind.Object ||
                    !validation.TryGetProperty("isvalid", out JsonElement isValid) || isValid.ValueKind != JsonValueKind.True ||
                    !validation.TryGetProperty("isgeocoin", out JsonElement isGeoCoin) || isGeoCoin.ValueKind != JsonValueKind.True)
                    throw new InvalidOperationException("Этот QR-код содержит адрес другой сети или неверный адрес.");
            }
            else if (!address.StartsWith("GEO", StringComparison.OrdinalIgnoreCase) || address.Any(character => !char.IsLetterOrDigit(character)))
            {
                throw new InvalidOperationException("QR-код не содержит адрес GeoCoin.");
            }

            _sendAddress.Text = address;
            if (!string.IsNullOrWhiteSpace(amountText))
            {
                string normalizedAmount = amountText.Trim().Replace(',', '.');
                if (decimal.TryParse(normalizedAmount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount) &&
                    amount > 0 && decimal.Round(amount, 8) == amount)
                    _sendAmount.Text = amount.ToString("0.########", UiLocalization.Culture(_uiLanguage));
            }
            _sendMessage.Text = "Адрес загружен из QR-кода. Перед отправкой проверьте адрес и сумму.";
            _sendMessage.ForeColor = Success;
            ApplyUiLanguage(_sendMessage);
        }
        catch (Exception ex)
        {
            _sendMessage.Text = "Не удалось импортировать QR-код: " + ex.Message;
            _sendMessage.ForeColor = Error;
            ApplyUiLanguage(_sendMessage);
        }
    }

    private async Task RefreshReceiveAddressesAsync(string? preferredAddress = null)
    {
        if (_loadingReceiveAddresses || _rpc is null || _currentPage != "receive" || !File.Exists(AppPaths.WalletPath)) return;
        _loadingReceiveAddresses = true;
        _receiveAddressCombo.Enabled = false;
        _receiveMessage.Text = "Загружаю адреса текущего кошелька…";
        try
        {
            string? previous = preferredAddress ?? _receiveAddressCombo.SelectedItem as string ?? _lastAddress;
            List<string> addresses = await GetWalletAddressesAsync();
            if (!string.IsNullOrWhiteSpace(preferredAddress) && !addresses.Contains(preferredAddress, StringComparer.Ordinal))
                addresses.Add(preferredAddress);

            _receiveAddressCombo.BeginUpdate();
            _receiveAddressCombo.Items.Clear();
            foreach (string address in addresses) _receiveAddressCombo.Items.Add(address);
            int selected = previous is null ? -1 : addresses.FindIndex(address => address == previous);
            _receiveAddressCombo.SelectedIndex = selected >= 0 ? selected : (addresses.Count > 0 ? 0 : -1);
            _receiveAddressCombo.EndUpdate();

            if (_receiveAddressCombo.SelectedItem is string selectedAddress)
            {
                _lastAddress = selectedAddress;
                _receiveMessage.Text = "Выберите адрес для перевода или создайте новый. QR-код содержит только адрес.";
            }
            else
            {
                _lastAddress = null;
                _receiveMessage.Text = "В кошельке нет адресов. Создайте новый адрес для получения монет.";
            }
            UpdateReceiveAddressDisplay();
        }
        catch (Exception ex)
        {
            _receiveMessage.Text = "Не удалось загрузить адреса кошелька: " + ex.Message;
            UpdateReceiveAddressDisplay();
        }
        finally
        {
            _loadingReceiveAddresses = false;
            if (_currentPage == "receive" && !IsDisposed)
                _receiveAddressCombo.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        }
    }

    private void UpdateReceiveAddressDisplay()
    {
        string? address = _receiveAddressCombo.SelectedItem as string ?? _lastAddress;
        _receiveAddress.Text = string.IsNullOrWhiteSpace(address) ? "В кошельке пока нет адреса" : address;
        _receiveCopyButton.Enabled = !string.IsNullOrWhiteSpace(address);
        _saveReceiveQrButton.Enabled = !string.IsNullOrWhiteSpace(address);

        Image? oldImage = _receiveQrCode.Image;
        _receiveQrCode.Image = null;
        oldImage?.Dispose();
        if (string.IsNullOrWhiteSpace(address))
        {
            _receiveQrCode.Visible = false;
            _receiveQrCaption.Visible = false;
            return;
        }

        try
        {
            using QRCodeData data = QRCodeGenerator.GenerateQrCode(address, QRCodeGenerator.ECCLevel.Q);
            using var renderer = new PngByteQRCode(data);
            byte[] png = renderer.GetGraphic(8);
            using var stream = new MemoryStream(png);
            using Image image = Image.FromStream(stream);
            _receiveQrCode.Image = new Bitmap(image);
            _receiveQrCode.Visible = true;
            _receiveQrCaption.Visible = true;
            _saveReceiveQrButton.Enabled = true;
        }
        catch (Exception ex)
        {
            _receiveQrCode.Visible = false;
            _receiveQrCaption.Visible = false;
            _saveReceiveQrButton.Enabled = false;
            WriteNodeLog("Не удалось подготовить QR-код адреса: " + ex.Message);
        }
    }

    private async Task RefreshTransactionsAsync()
    {
        if (_loadingTransactions || _currentPage != "transactions" || _transactionGrid is null) return;
        if (_rpc is null)
        {
            _transactionsAutoLoadPending = true;
            return;
        }
        _transactionsAutoLoadPending = false;
        DataGridView grid = _transactionGrid;
        _loadingTransactions = true;
        _refreshTransactionsButton.Enabled = false;
        _transactionStatus.Text = TranslateUiText("Обновляю историю кошелька…");
        try
        {
            JsonElement result = await _rpc.CallAsync("listtransactions", new object[] { "*", 1000, 0, true });
            if (!ReferenceEquals(grid, _transactionGrid) || _currentPage != "transactions") return;
            if (result.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Нода вернула историю в неизвестном формате.");
            _transactionEntries = result.EnumerateArray()
                .Select(entry => entry.Clone())
                .OrderByDescending(GetTransactionSortTimestamp)
                .ThenBy(entry => ReadInt64(entry, "confirmations"))
                .ToList();
            _transactionsPageIndex = 0;
            RenderTransactionsPage(grid);
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(grid, _transactionGrid))
                _transactionStatus.Text = TranslateUiText("Не удалось загрузить историю: " + ex.Message);
        }
        finally
        {
            _loadingTransactions = false;
            if (_currentPage == "transactions" && !IsDisposed)
                _refreshTransactionsButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        }
    }

    private int GetTransactionPageCount()
        => Math.Max(1, (int)Math.Ceiling(_transactionEntries.Count / (double)Math.Max(1, _transactionsPageSize)));

    private void RenderTransactionsPage() => RenderTransactionsPage(_transactionGrid);

    private void RenderTransactionsPage(DataGridView? grid)
    {
        if (grid is null) return;
        int pageCount = GetTransactionPageCount();
        _transactionsPageIndex = Math.Clamp(_transactionsPageIndex, 0, pageCount - 1);
        int firstEntryIndex = _transactionsPageIndex * _transactionsPageSize;
        List<JsonElement> page = _transactionEntries.Skip(firstEntryIndex).Take(_transactionsPageSize).ToList();

        grid.Rows.Clear();
        foreach (JsonElement entry in page)
        {
            string txid = ReadString(entry, "txid");
            string category = ReadString(entry, "category").ToLowerInvariant();
            string address = ReadString(entry, "address");
            string operation = category switch
            {
                "receive" => "Получение",
                "send" => "Отправка",
                "generate" => "Майнинг",
                "immature" => "Награда",
                "orphan" => "Отменена",
                _ => string.IsNullOrWhiteSpace(category) ? "Операция" : category
            };
            decimal amount = ReadDecimal(entry, "amount");
            decimal fee = ReadDecimal(entry, "fee");
            long confirmations = ReadInt64(entry, "confirmations");
            string status = category switch
            {
                "immature" => "Ожидает зрелости",
                "orphan" => "Отменена",
                _ when confirmations < 0 => "Конфликт",
                _ when confirmations == 0 => "Ожидает",
                _ => $"{confirmations:N0} подтв."
            };
            string date = FormatTransactionTime(entry);
            int rowIndex = grid.Rows.Add(date, TranslateUiText(operation), FormatGcAmount(amount, includePositiveSign: true),
                fee == 0 ? "—" : Math.Abs(fee).ToString("0.########", UiLocalization.Culture(_uiLanguage)) + " GC", TranslateUiText(status), address);
            DataGridViewRow row = grid.Rows[rowIndex];
            row.Tag = txid;
            row.Cells[5].ToolTipText = address;
            row.Cells[2].Style.ForeColor = amount < 0 ? Error : Success;
        }

        if (grid.Rows.Count > 0)
        {
            grid.ClearSelection();
            grid.CurrentCell = grid.Rows[0].Cells[0];
            grid.Rows[0].Selected = true;
            _selectedTransactionId = grid.Rows[0].Tag as string;
            _transactionDetails.Text = string.IsNullOrWhiteSpace(_selectedTransactionId)
                ? TranslateUiText("Выберите запись, чтобы посмотреть номер транзакции.")
                : TranslateUiText("ID транзакции: " + _selectedTransactionId);
            _copyTransactionIdButton.Enabled = !string.IsNullOrWhiteSpace(_selectedTransactionId);
            UpdateTransactionExplorerLink();
        }
        else
        {
            _selectedTransactionId = null;
            _transactionDetails.Text = TranslateUiText("Выберите запись, чтобы посмотреть номер транзакции.");
            _copyTransactionIdButton.Enabled = false;
            UpdateTransactionExplorerLink();
        }

        if (_transactionEntries.Count == 0)
            _transactionStatus.Text = TranslateUiText("В истории пока нет операций. После синхронизации обновите список.");
        else
        {
            int first = firstEntryIndex + 1;
            int last = firstEntryIndex + grid.Rows.Count;
            _transactionStatus.Text = TranslateUiText("Показаны записи ") +
                $"{first}–{last} / {_transactionEntries.Count}";
        }
        _transactionsPageLabel.Text = $"{_transactionsPageIndex + 1}/{pageCount}";
        _previousTransactionsPageButton.Enabled = _transactionsPageIndex > 0;
        _nextTransactionsPageButton.Enabled = _transactionsPageIndex + 1 < pageCount;
    }

    private static long GetTransactionSortTimestamp(JsonElement entry)
    {
        foreach (string propertyName in new[] { "time", "timereceived", "blocktime" })
        {
            if (!entry.TryGetProperty(propertyName, out JsonElement value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long timestamp)) return timestamp;
            if (value.ValueKind == JsonValueKind.String)
            {
                string text = value.GetString() ?? "";
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp)) return timestamp;
                if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed))
                    return parsed.ToUnixTimeSeconds();
            }
        }
        return 0;
    }

    private void CopySelectedTransactionId()
    {
        if (string.IsNullOrWhiteSpace(_selectedTransactionId)) return;
        try
        {
            Clipboard.SetText(_selectedTransactionId);
            _transactionStatus.Text = "ID транзакции скопирован.";
        }
        catch (Exception ex)
        {
            _transactionStatus.Text = "Не удалось скопировать ID: " + ex.Message;
        }
    }

    private void UpdateTransactionExplorerLink()
    {
        _openTransactionExplorerLink.Text = TranslateUiText("Открыть в блокчейн-обозревателе ↗");
        _openTransactionExplorerLink.LinkArea = new LinkArea(0, _openTransactionExplorerLink.Text.Length);
        _openTransactionExplorerLink.Visible = BuildTransactionExplorerUri(_selectedTransactionId) is not null;
    }

    private void OpenSelectedTransactionInExplorer()
    {
        Uri? uri = BuildTransactionExplorerUri(_selectedTransactionId);
        if (uri is null) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            WriteNodeLog("Не удалось открыть обозреватель транзакций: " + ex.Message);
            ShowLocalizedMessage("Не удалось открыть обозреватель. Проверьте подключение к интернету.",
                "Не удалось открыть обозреватель", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static Uri? BuildTransactionExplorerUri(string? transactionId)
    {
        if (!string.Equals(AppPaths.NetworkId, "degree", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(transactionId) || transactionId.Length != 64 ||
            transactionId.Any(character => !Uri.IsHexDigit(character)))
            return null;

        return new Uri($"{AppPaths.TransactionExplorerBaseUrl}/tx/{Uri.EscapeDataString(transactionId)}", UriKind.Absolute);
    }

    private async Task RefreshWalletAddressesAsync()
    {
        if (_loadingWalletAddresses || _rpc is null || _currentPage != "wallet" || !File.Exists(AppPaths.WalletPath)) return;
        _loadingWalletAddresses = true;
        _refreshWalletAddressesButton.Enabled = false;
        _walletAddressCombo.Enabled = false;
        _walletKeyStatus.Text = "Загружаю адреса текущего кошелька…";
        try
        {
            string? previous = _walletAddressCombo.SelectedItem as string;
            List<string> addresses = await GetWalletAddressesAsync();
            _walletAddresses.Clear();
            _walletAddresses.AddRange(addresses);
            _walletAddressCombo.BeginUpdate();
            _walletAddressCombo.Items.Clear();
            foreach (string address in addresses) _walletAddressCombo.Items.Add(address);
            int selected = previous is null ? -1 : addresses.FindIndex(address => address == previous);
            _walletAddressCombo.SelectedIndex = selected >= 0 ? selected : (addresses.Count > 0 ? 0 : -1);
            _walletAddressCombo.EndUpdate();
            _walletKeyStatus.Text = addresses.Count == 0
                ? "В кошельке нет доступных адресов."
                : $"Найдено адресов: {addresses.Count}. Экспорт ключа создаёт незашифрованный текстовый файл.";
        }
        catch (Exception ex)
        {
            _walletAddresses.Clear();
            _walletAddressCombo.Items.Clear();
            _walletKeyExportButton.Enabled = false;
            _walletKeyStatus.Text = "Не удалось загрузить адреса: " + ex.Message;
        }
        finally
        {
            _loadingWalletAddresses = false;
            if (_currentPage == "wallet" && !IsDisposed)
            {
                _walletAddressCombo.Enabled = true;
                _refreshWalletAddressesButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
                _walletKeyExportButton.Enabled = IsNodeRunning() && _walletAddressCombo.SelectedItem is string;
            }
        }
    }

    private async Task<List<string>> GetWalletAddressesAsync()
    {
        if (_rpc is null) throw new InvalidOperationException("Нода GeoCoin не подключена.");
        const int pageSize = 5000;
        var addresses = new List<string>();
        for (int offset = 0; offset < 100_000; offset += pageSize)
        {
            JsonElement result = await _rpc.CallAsync("getaddressesbyaccount", new object[] { "*", offset, pageSize });
            if (result.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Нода вернула список адресов в неизвестном формате.");
            int count = 0;
            foreach (JsonElement item in result.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                string address = item.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(address)) addresses.Add(address);
                count++;
            }
            if (count < pageSize) break;
        }
        return addresses.Distinct(StringComparer.Ordinal).ToList();
    }

    private async Task ImportPrivateKeyAsync()
    {
        if (!IsNodeRunning() || _rpc is null || !File.Exists(AppPaths.WalletPath)) return;
        string privateKey = _privateKeyInput.Text.Trim();
        if (privateKey.Length != 64 || privateKey.Any(character => !Uri.IsHexDigit(character)))
        {
            _walletKeyStatus.Text = "Проверьте ключ: нужно ровно 64 шестнадцатеричных символа 0–9, A–F.";
            return;
        }
        if (ShowLocalizedMessage("Этот ключ будет добавлен в текущий кошелёк. Импорт восстановит историю адреса и может занять время. Продолжить?",
                "Импорт приватного ключа", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        string? password = PasswordDialog.AskForAction(this, "импортировать приватный ключ", TranslateUiText);
        if (password is null) return;

        _walletKeyImportButton.Enabled = false;
        _walletKeyStatus.Text = "Импортирую ключ и восстанавливаю историю…";
        try
        {
            JsonElement result = await _rpc.CallAsync("importprivkey", new object[] { privateKey, "GeoCoin-Start", password, true, 0 });
            string address = ReadString(result, "address");
            bool rescanComplete = result.TryGetProperty("rescan_complete", out JsonElement complete) && complete.ValueKind == JsonValueKind.True;
            string scanStatus = ReadString(result, "rescan_status");
            _lastAddress = address;
            ResetWalletHistoryBackfill();
            _privateKeyInput.Clear();
            string importStatus = rescanComplete
                ? "Ключ добавлен. История адреса восстановлена."
                : $"Ключ добавлен, но история восстановлена не полностью ({scanStatus}).";
            await RefreshWalletAddressesAsync();
            await RefreshStateAsync();
            _walletKeyStatus.Text = importStatus;
        }
        catch (Exception ex)
        {
            _walletKeyStatus.Text = "Не удалось импортировать ключ: " + ex.Message;
        }
        finally
        {
            _walletKeyImportButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        }
    }

    private async Task ExportPrivateKeyAsync()
    {
        if (!IsNodeRunning() || _rpc is null || _walletAddressCombo.SelectedItem is not string address) return;
        if (ShowLocalizedMessage("Ключ даёт полный контроль над монетами этого адреса. Он будет записан в незашифрованный текстовый файл. Любой, кто получит файл, сможет потратить эти монеты. Продолжить?",
                "Экспорт секретного ключа", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        using var dialog = new SaveFileDialog
        {
            Title = "Куда сохранить приватный ключ",
            FileName = "GeoCoin-private-key.txt",
            Filter = "Текстовый файл (*.txt)|*.txt|Все файлы (*.*)|*.*",
            AddExtension = true,
            OverwritePrompt = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (File.Exists(dialog.FileName))
        {
            ShowLocalizedMessage("Выберите новый файл. Экспорт приватного ключа не перезаписывает существующие файлы.",
                "Файл уже существует", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string? password = PasswordDialog.AskForAction(this, "экспортировать приватный ключ выбранного адреса", TranslateUiText);
        if (password is null) return;

        _walletKeyExportButton.Enabled = false;
        _walletKeyStatus.Text = "Экспортирую ключ выбранного адреса…";
        try
        {
            JsonElement result = await _rpc.CallAsync("dumpprivkey", new object[] { address, password });
            string privateKey = result.ValueKind == JsonValueKind.String ? result.GetString() ?? "" : "";
            if (privateKey.Length != 64 || privateKey.Any(character => !Uri.IsHexDigit(character)))
                throw new InvalidOperationException("Оригинальная нода вернула ключ в неожиданном формате.");
            await File.WriteAllTextAsync(dialog.FileName, privateKey + Environment.NewLine, new UTF8Encoding(false));
            _walletKeyStatus.Text = "Ключ сохранён в выбранный файл. Храните его отдельно и никому не передавайте.";
            ShowLocalizedMessage("Приватный ключ сохранён. Он открывает только выбранный адрес; файл не зашифрован.",
                "Экспорт завершён", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _walletKeyStatus.Text = "Не удалось экспортировать ключ: " + ex.Message;
        }
        finally
        {
            _walletKeyExportButton.Enabled = IsNodeRunning() && _walletAddressCombo.SelectedItem is string;
        }
    }

    private async Task<FeeEstimate> CalculateSendFeeEstimateAsync()
    {
        if (!IsNodeRunning() || _rpc is null)
            throw new InvalidOperationException("Сначала запустите ноду, чтобы рассчитать комиссию.");

        JsonElement feeInfo = await _rpc.CallAsync("getfeerate");
        decimal minimum = ReadDecimal(feeInfo, "min_tx_fee_gc");
        decimal ratePerKbGc = ReadDecimal(feeInfo, "recommended_fee_rate_per_kb_gc");
        if (ratePerKbGc <= 0 && feeInfo.TryGetProperty("recommended_fee_rate_per_kb", out JsonElement angleRate) &&
            angleRate.TryGetDecimal(out decimal rateAngles))
            ratePerKbGc = rateAngles / 100_000_000m;
        if (minimum <= 0 && feeInfo.TryGetProperty("min_tx_fee", out JsonElement minimumAngles) &&
            minimumAngles.TryGetDecimal(out decimal minimumAngleValue))
            minimum = minimumAngleValue / 100_000_000m;
        if (minimum <= 0)
            throw new InvalidOperationException("Нода не сообщила минимальную комиссию.");

        // GeoCoin's wallet estimate uses 40 base + 1,800 per input + 60 per output.
        // Assume one input and recipient plus change outputs, matching a typical send.
        const int typicalSerializedBytes = 1960;
        decimal estimated = Math.Max(minimum, ratePerKbGc * typicalSerializedBytes / 1000m);
        estimated = decimal.Ceiling(estimated * 100_000_000m) / 100_000_000m;
        return new FeeEstimate(estimated, minimum, Math.Max(0, ratePerKbGc), typicalSerializedBytes);
    }

    private string FormatSendFeeAmount(decimal amount)
        => amount.ToString("0.########", UiLocalization.Culture(_uiLanguage));

    private string FormatSendFeeEstimate(FeeEstimate estimate)
    {
        string approximate = FormatSendFeeAmount(estimate.ApproximateGc);
        string minimum = FormatSendFeeAmount(estimate.MinimumGc);
        string rate = FormatSendFeeAmount(estimate.RateGcPerKb);
        if (_uiLanguage == "en")
        {
            _sendFeeHint.Text = $"Minimum {minimum} GC · rate {rate} GC/kB · typical transaction about {estimate.AssumedBytes} bytes.";
            return $"Estimated fee: about {approximate} GC";
        }
        if (_uiLanguage == "zh-CN")
        {
            _sendFeeHint.Text = $"最低 {minimum} GC · 费率 {rate} GC/千字节 · 按约 {estimate.AssumedBytes} 字节的普通交易估算。";
            return $"预计手续费：约 {approximate} GC";
        }
        if (_uiLanguage == "pt-BR")
        {
            _sendFeeHint.Text = $"Mínimo {minimum} GC · taxa {rate} GC/kB · transação típica de cerca de {estimate.AssumedBytes} bytes.";
            return $"Taxa estimada: cerca de {approximate} GC";
        }
        if (_uiLanguage == "it")
        {
            _sendFeeHint.Text = $"Minimo {minimum} GC · tariffa {rate} GC/kB · transazione tipica di circa {estimate.AssumedBytes} byte.";
            return $"Commissione stimata: circa {approximate} GC";
        }
        _sendFeeHint.Text = $"Минимум {minimum} GC · ставка {rate} GC/кБ · обычная транзакция около {estimate.AssumedBytes} байт.";
        return $"Ориентировочная комиссия: {approximate} GC";
    }

    private async Task RefreshSendFeeEstimateAsync()
    {
        if (_refreshingSendFee || _currentPage != "send" || !IsNodeRunning() || _sendFeeEstimate.IsDisposed) return;
        _refreshingSendFee = true;
        try
        {
            FeeEstimate estimate = await CalculateSendFeeEstimateAsync();
            if (_currentPage != "send" || _sendFeeEstimate.IsDisposed) return;
            _sendFeeEstimate.Text = FormatSendFeeEstimate(estimate);
            ApplyUiLanguage(_sendFeeEstimate);
            ApplyUiLanguage(_sendFeeHint);
        }
        catch (Exception ex)
        {
            if (_currentPage == "send" && !_sendFeeEstimate.IsDisposed)
            {
                _sendFeeEstimate.Text = "Не удалось получить оценку комиссии.";
                _sendFeeHint.Text = ex.Message;
                ApplyUiLanguage(_sendFeeEstimate);
                ApplyUiLanguage(_sendFeeHint);
            }
        }
        finally
        {
            _refreshingSendFee = false;
        }
    }

    private bool _sendingTransfer;

    private async Task SendAsync()
    {
        if (_sendingTransfer || !IsNodeRunning() || _rpc is not { } rpc) return;
        _sendingTransfer = true;
        _sendButton.Enabled = false;
        try { await ReviewAndSendAsync(rpc); }
        finally
        {
            _sendingTransfer = false;
            _sendButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        }
    }

    private async Task ReviewAndSendAsync(GeoCoinRpcClient rpc)
    {
        string address = _sendAddress.Text.Trim();
        string rawAmount = _sendAmount.Text.Trim().Replace(',', '.');
        if (address.Length < 20)
        {
            _sendMessage.Text = "Проверьте адрес получателя.";
            _sendMessage.ForeColor = Error;
            return;
        }
        if (!decimal.TryParse(rawAmount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
        {
            _sendMessage.Text = "Введите сумму больше нуля, используя точку или запятую.";
            _sendMessage.ForeColor = Error;
            return;
        }
        if (decimal.Round(amount, 8) != amount)
        {
            _sendMessage.Text = "Сумма может содержать не больше 8 знаков после запятой.";
            _sendMessage.ForeColor = Error;
            return;
        }
        string password = _sendPassword.Text;
        if (password.Length == 0)
        {
            _sendMessage.Text = "Введите пароль кошелька.";
            _sendMessage.ForeColor = Error;
            return;
        }

        FeeEstimate feeEstimate;
        try
        {
            feeEstimate = await CalculateSendFeeEstimateAsync();
            _sendFeeEstimate.Text = FormatSendFeeEstimate(feeEstimate);
            ApplyUiLanguage(_sendFeeEstimate);
            ApplyUiLanguage(_sendFeeHint);
        }
        catch (Exception ex)
        {
            _sendMessage.Text = "Не удалось рассчитать комиссию: " + ex.Message;
            _sendMessage.ForeColor = Error;
            return;
        }

        string amountForReview = amount.ToString("0.########", UiLocalization.Culture(_uiLanguage));
        string review = _uiLanguage switch
        {
            "en" => $"Send {amountForReview} GC?\n\nRecipient:\n{address}\n\n" +
                    $"Fee: about {FormatSendFeeAmount(feeEstimate.ApproximateGc)} GC\n" +
                    $"Minimum: {FormatSendFeeAmount(feeEstimate.MinimumGc)} GC. This estimate assumes a typical {feeEstimate.AssumedBytes}-byte transaction; the final fee depends on the selected inputs and may be higher.\n\n" +
                    "A confirmed transfer cannot be canceled.",
            "zh-CN" => $"发送 {amountForReview} GC？\n\n收款地址：\n{address}\n\n" +
                       $"手续费：约 {FormatSendFeeAmount(feeEstimate.ApproximateGc)} GC\n" +
                       $"最低：{FormatSendFeeAmount(feeEstimate.MinimumGc)} GC。估算按约 {feeEstimate.AssumedBytes} 字节的普通交易计算；实际费用取决于所选输入，可能更高。\n\n" +
                       "确认后的转账无法撤销。",
            "pt-BR" => $"Enviar {amountForReview} GC?\n\nDestinatário:\n{address}\n\n" +
                       $"Taxa: cerca de {FormatSendFeeAmount(feeEstimate.ApproximateGc)} GC\n" +
                       $"Mínimo: {FormatSendFeeAmount(feeEstimate.MinimumGc)} GC. A estimativa considera uma transação típica de {feeEstimate.AssumedBytes} bytes; a taxa final depende das entradas selecionadas e pode ser maior.\n\n" +
                       "Uma transferência confirmada não pode ser cancelada.",
            "it" => $"Inviare {amountForReview} GC?\n\nDestinatario:\n{address}\n\n" +
                    $"Commissione: circa {FormatSendFeeAmount(feeEstimate.ApproximateGc)} GC\n" +
                    $"Minimo: {FormatSendFeeAmount(feeEstimate.MinimumGc)} GC. La stima considera una transazione tipica di {feeEstimate.AssumedBytes} byte; la commissione finale dipende dagli input selezionati e potrebbe essere maggiore.\n\n" +
                    "Una transazione confermata non può essere annullata.",
            _ => $"Отправить {amountForReview} GC?\n\nПолучатель:\n{address}\n\n" +
                 $"Комиссия: примерно {FormatSendFeeAmount(feeEstimate.ApproximateGc)} GC\n" +
                 $"Минимум: {FormatSendFeeAmount(feeEstimate.MinimumGc)} GC. Расчёт сделан для обычной транзакции около {feeEstimate.AssumedBytes} байт; точная сумма зависит от выбранных входов и может быть выше.\n\n" +
                 "После подтверждения перевод нельзя отменить."
        };
        string reviewTitle = _uiLanguage switch { "en" => "Review transfer", "zh-CN" => "确认转账", "pt-BR" => "Revise a transferência", "it" => "Conferma il trasferimento", _ => "Проверьте перевод" };
        if (ShowLocalizedMessage(review, reviewTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        if (!ReferenceEquals(rpc, _rpc) || !IsNodeRunning())
        {
            _sendMessage.Text = TranslateUiText("Подключение к ноде изменилось. Проверьте перевод заново.");
            _sendMessage.ForeColor = Error;
            return;
        }
        _sendMessage.Text = TranslateUiText("Отправляю перевод…");
        _sendMessage.ForeColor = Muted;
        try
        {
            // The node recalculates the fee from selected inputs and applies at least this estimate.
            var parameters = new object[] { address, amount, feeEstimate.ApproximateGc, password };
            JsonElement result = await rpc.CallAsync("sendtoaddress", parameters);
            string txid = result.ValueKind == JsonValueKind.String ? result.GetString() ?? "" : result.ToString();
            _sendMessage.Text = TranslateUiText("Перевод принят нодой. Номер: " + txid);
            _sendMessage.ForeColor = Success;
            if (_notificationsEnabled && _notifyOutgoingTransfers)
                ShowSystemNotification("Перевод отправлен",
                    "Нода приняла перевод к отправке. Он ещё не подтверждён в блоке.");
            _sendPassword.Clear();
            await RefreshStateAsync();
        }
        catch (Exception ex)
        {
            _sendMessage.Text = ex.Message;
            _sendMessage.ForeColor = Error;
        }
    }

    private async Task RefreshStateAsync()
    {
        if (_refreshing) return;
        bool rpcWasHealthy = _rpcHealthy;
        if (!IsNodeRunning() || _rpc is null)
        {
            _rpcHealthy = false;
            if (_nodeProcess is not null && !IsNodeProcessRunning())
            {
                if (_notificationBaselineReady && _notifyNodeStopped)
                    ShowSystemNotification("Нода остановлена", "Процесс GeoCoin завершился. Откройте вкладку «Нода», чтобы посмотреть журнал.", ToolTipIcon.Warning);
                _nodeStatus = "Нода завершилась";
                CleanupNodeSession();
                UpdateStatus();
                if (_currentPage is "overview" or "send" or "receive" or "transactions" or "wallet") BuildPage();
            }
            if (_currentPage == "node") UpdateNodePage();
            return;
        }
        _refreshing = true;
        try
        {
            JsonElement chain = await _rpc.CallAsync("getblockchaininfo");
            _rpcHealthy = true;
            _networkFailureNotified = false;
            _height = ReadInt64(chain, "blocks");
            _headers = ReadInt64(chain, "headers");
            try
            {
                JsonElement connections = await _rpc.CallAsync("getconnectioncount");
                _connections = connections.ValueKind == JsonValueKind.Number && connections.TryGetInt32(out int count)
                    ? count
                    : 0;
            }
            catch { }

            if (File.Exists(AppPaths.WalletPath))
            {
                try
                {
                    JsonElement balance = await _rpc.CallAsync("getbalance");
                    if (balance.ValueKind == JsonValueKind.Number && balance.TryGetDecimal(out decimal value))
                        _balance = value;
                }
                catch { }
                try
                {
                    JsonElement walletInfo = await _rpc.CallAsync("getwalletinfo");
                    _matureCoins = ReadDecimal(walletInfo, "balance");
                    _immatureCoins = ReadDecimal(walletInfo, "immature_balance");
                }
                catch { }
            }

            if (_minerRpc is not null)
            {
                try
                {
                    JsonElement mining = await _minerRpc.CallAsync("getmininginfo");
                    _cpuMiningActive = mining.TryGetProperty("generate", out JsonElement generate) &&
                        generate.ValueKind == JsonValueKind.True;
                    _cpuHashrateValue = _cpuMiningActive ? ReadDouble(mining, "hashrate") : 0;
                    UpdateMiningSessionState();
                }
                catch { }
            }

            int blocksBefore;
            lock (_minedBlocksLock) blocksBefore = _lifetimeMinedBlockIds.Count;
            bool hadNotificationBaseline = _notificationBaselineReady;
            string previousNodeStatus = _nodeStatus;
            await RefreshLifetimeWalletMinedBlocksAsync();
            await RefreshWalletMinedBlocksAsync();
            _nodeStatus = _headers > 0 && _height >= _headers ? "Синхронизация завершена" : "Синхронизация";
            int blocksAfter;
            lock (_minedBlocksLock) blocksAfter = _lifetimeMinedBlockIds.Count;
            if (hadNotificationBaseline && _notifyMiningRewards && blocksAfter > blocksBefore)
                ShowSystemNotification("Новая награда GeoCoin", "История кошелька показывает новый добытый блок.");
            if (_notifySyncComplete &&
                _nodeStatus == "Синхронизация завершена" && previousNodeStatus != "Синхронизация завершена")
                ShowSystemNotification("Синхронизация завершена", "Нода догнала текущую высоту сети.");
            _notificationBaselineReady = true;
        }
        catch
        {
            _rpcHealthy = false;
            if (rpcWasHealthy && _notificationBaselineReady && !_networkFailureNotified && _notifyNodeStopped)
            {
                ShowSystemNotification("Потеряно соединение с нодой", "RPC не отвечает. Проверьте состояние ноды и журнал.", ToolTipIcon.Warning);
                _networkFailureNotified = true;
            }
            if (_nodeProcess is not null && !IsNodeProcessRunning())
            {
                _nodeStatus = "Нода остановлена";
                CleanupNodeSession();
            }
        }
        finally
        {
            _refreshing = false;
            UpdateStatus();
            if (_currentPage == "overview")
            {
                _balanceValue.Text = File.Exists(AppPaths.WalletPath)
                    ? FormatGcAmount(_balance)
                    : "Кошелёк ещё не создан";
                _nodeState.Text = _nodeStatus;
                _syncState.Text = SyncDescription();
                _peerState.Text = FormatOverviewPeerState();
                UpdateOverviewMiningSummary();
            }
            RefreshMiningControls();
            if (_currentPage == "node") UpdateNodePage();
            if (_applicationLocked) UpdateLockScreenMetrics();
            if (_transactionsAutoLoadPending && _currentPage == "transactions" && !_loadingTransactions &&
                _rpc is not null && File.Exists(AppPaths.WalletPath))
                _ = RefreshTransactionsAsync();
        }
    }

    private void RefreshMiningControls()
    {
        if (_currentPage == "subminer" && !IsDisposed)
        {
            _gpuMiningState.Text = TranslateUiText(_gpuStatusText);
            _miningHashrate.Text = TranslateUiText(_gpuHashrateText);
            _remoteGpuButton.Text = TranslateUiText(_remoteGpuMining ? "Остановить подмайнер" : _startingRemoteGpu ? "Подключаюсь…" : "Подключить и начать GPU-майнинг");
            _remoteGpuButton.Enabled = _remoteGpuMining || (!_startingRemoteGpu && !_checkingGpuDevices && !IsNodeRunning() &&
                _gpuMinerProcess is null && SelectedGpuDevices().Count > 0);
            _remoteGpuBackendCombo.Enabled = !_remoteGpuMining && !_startingRemoteGpu;
            _remoteGpuDeviceList.Enabled = !_remoteGpuMining && !_startingRemoteGpu && _remoteGpuDeviceList.Items.Count > 0;
            _remoteGpuFindButton.Enabled = !_remoteGpuMining && !_checkingGpuDevices;
        }
        if (_currentPage != "mining" || IsDisposed) return;
        _miningState.Text = TranslateUiText(_cpuMiningActive
            ? $"CPU-майнинг включён · потоков: {_selectedThreads}"
            : "CPU-майнинг остановлен");
        _miningState.ForeColor = _cpuMiningActive ? Success : Muted;
        _cpuHashrate.Text = TranslateUiText(_cpuMiningActive
            ? $"Скорость CPU: {FormatHashrate(_cpuHashrateValue)}"
            : "Скорость CPU: —");
        _threadCount.Enabled = !_cpuMiningActive;
        _mineButton.Text = TranslateUiText(_cpuMiningActive ? "Остановить CPU" : "Запустить CPU");
        _mineButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);

        _gpuMiningState.Text = TranslateUiText(_gpuStatusText);
        _gpuMiningState.ForeColor = _gpuMiningActive ? Success : Muted;
        _miningHashrate.Text = TranslateUiText(_gpuHashrateText);
        _gpuRewardAddress.Text = string.IsNullOrWhiteSpace(_lastAddress)
            ? TranslateUiText("Адрес награды будет взят из текущего кошелька.")
            : TranslateUiText("Адрес награды: " + _lastAddress);
        _gpuMineButton.Text = TranslateUiText(_remoteGpuMining ? "Перейти к подмайнеру" : _gpuMiningActive ? "Остановить GPU" : "Запустить GPU");
        _gpuMineButton.Enabled = _remoteGpuMining || (IsNodeRunning() && File.Exists(AppPaths.WalletPath) &&
            (_gpuMiningActive || SelectedGpuDevices().Count > 0));
        _gpuBackendCombo.Enabled = !_gpuMiningActive;
        _gpuDeviceList.Enabled = !_gpuMiningActive && _gpuDeviceList.Items.Count > 0;
        _refreshGpuButton.Enabled = !_gpuMiningActive && !_checkingGpuDevices;
    }

    private void UpdateOverviewMiningSummary()
    {
        if (_lifetimeMinedBlocksLabel.IsDisposed || _matureCoinsLabel.IsDisposed || _immatureCoinsLabel.IsDisposed) return;
        int lifetimeCount;
        lock (_minedBlocksLock) lifetimeCount = _lifetimeMinedBlockIds.Count;
        _lifetimeMinedBlocksLabel.Text = $"Добыто блоков за всё время: {lifetimeCount:N0}";
        if (IsNodeRunning() && File.Exists(AppPaths.WalletPath))
        {
            _matureCoinsLabel.Text = "Зрелые монеты: " + FormatGcAmount(_matureCoins);
            _immatureCoinsLabel.Text = "Незрелые монеты: " + FormatGcAmount(_immatureCoins);
        }
        else
        {
            _matureCoinsLabel.Text = "Зрелые монеты: —";
            _immatureCoinsLabel.Text = "Незрелые монеты: —";
        }
    }

    private string SyncDescription()
    {
        if (!IsNodeRunning()) return "Запустите ноду, чтобы начать подключение";
        if (_headers <= 0) return "Подключаюсь к сети GeoCoin…";
        if (_height >= _headers) return "Синхронизация завершена";
        decimal percent = Math.Clamp((decimal)_height * 100m / _headers, 0m, 100m);
        return $"Синхронизировано {percent:0.0}%";
    }

    private void UpdateStatus()
    {
        bool processRunning = IsNodeProcessRunning();
        _lockNowButton.Visible = IsPinLockConfigured();
        _statusLabel.Text = processRunning ? _nodeStatus : "Нода остановлена";
        _statusLabel.Text = TranslateUiText(_statusLabel.Text);
        _statusLabel.ForeColor = processRunning
            ? (_nodeStatus.Contains("завершена", StringComparison.OrdinalIgnoreCase)
                ? Success : Accent)
            : Muted;
        _statusLabel.BackColor = processRunning
            ? (_modernDesign
                ? (_darkTheme ? Color.FromArgb(35, 61, 54) : Color.FromArgb(215, 238, 227))
                : (_darkTheme ? Color.FromArgb(48, 67, 53) : Color.FromArgb(229, 235, 221)))
            : (_modernDesign
                ? (_darkTheme ? Color.FromArgb(42, 51, 53) : Color.FromArgb(226, 234, 231))
                : (_darkTheme ? Color.FromArgb(48, 55, 50) : Color.FromArgb(226, 228, 219)));
        LayoutHeaderStatus();
    }

    private bool IsNodeProcessRunning()
    {
        if (_nodeProcess is null) return false;
        try { return !_nodeProcess.HasExited; }
        catch (InvalidOperationException) { return false; }
    }

    private bool IsNodeRunning() => IsNodeProcessRunning() && _rpc is not null;

    private void CleanupNodeSession()
    {
        _rpcHealthy = false;
        _rpc?.Dispose();
        _rpc = null;
        _minerRpc?.Dispose();
        _minerRpc = null;
        _adminPassword = null;
        _minerPassword = null;
        _minerPassFile = null;
        _cpuMiningActive = false;
        _cpuHashrateValue = 0;
        _gpuMiningActive = false;
        UpdateMiningSessionState();
        _gpuStatusText = "GPU-майнинг остановлен";
        _gpuHashrateText = "Скорость GPU: —";
        _lastAddress = null;
        _walletAddresses.Clear();
        _walletAddressCombo.Items.Clear();
        if (_gpuMinerProcess is { } miner)
        {
            try { if (!miner.HasExited) miner.Kill(entireProcessTree: true); } catch { }
            try { miner.Dispose(); } catch { }
            _gpuMinerProcess = null;
        }
        _remoteGpuMining = false;
        DeleteRemoteMinerPassFile();
        _logWriter?.Dispose();
        _logWriter = null;
        if (_nodeProcess is not null && !IsNodeProcessRunning())
        {
            _nodeProcess.Dispose();
            _nodeProcess = null;
        }
        if (_nodeProcess is null && !string.IsNullOrWhiteSpace(_sessionDirectory))
        {
            try { Directory.Delete(_sessionDirectory, recursive: true); } catch { }
            _sessionDirectory = null;
        }
        ReleaseNodePathAliasIfIdle();
    }

    private async void HandleClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose) return;
        if (e.CloseReason == CloseReason.UserClosing && !_trayExitRequested)
        {
            e.Cancel = true;
            HideToSystemTray();
            return;
        }
        if (_startingNode || _startingRemoteGpu)
        {
            e.Cancel = true;
            _trayExitRequested = false;
            ClearPendingUpdateInstaller(ref _updateInstallerToLaunch);
            ShowLocalizedMessage("Дождитесь завершения запуска узла или подмайнера, затем повторите выход.",
                "Выполняется запуск", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        bool gpuRunning = _gpuMinerProcess is { HasExited: false };
        if (!IsNodeRunning() && !gpuRunning)
        {
            _trayExitRequested = false;
            return;
        }
        e.Cancel = true;
        if (_closePromptOpen) return;
        _closePromptOpen = true;
        var result = ShowLocalizedMessage(gpuRunning && !IsNodeRunning()
                ? "Перед выходом остановить GPU-подмайнер?"
                : "Перед выходом остановить ноду GeoCoin и запущенный GPU-майнер?",
            "Закрытие GeoCoin-Start",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            if (_gpuMinerProcess is not null) await StopGpuMinerAsync();
            if (IsNodeRunning()) await StopNodeAsync();
            if (!IsNodeRunning() && _gpuMinerProcess is null)
            {
                _allowClose = true;
                Close();
            }
            else
            {
                _trayExitRequested = false;
                ClearPendingUpdateInstaller(ref _updateInstallerToLaunch);
            }
        }
        else
        {
            _trayExitRequested = false;
            ClearPendingUpdateInstaller(ref _updateInstallerToLaunch);
        }
        _closePromptOpen = false;
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == AppPaths.RequestInstallerShutdownMessage)
        {
            message.Result = new IntPtr(1);
            if (!IsDisposed && IsHandleCreated)
            {
                try { BeginInvoke((Action)CloseForInstallerUpdate); } catch (InvalidOperationException) { }
            }
            return;
        }
        base.WndProc(ref message);
    }

    private void CloseForInstallerUpdate()
    {
        _trayExitRequested = true;
        if (!Visible) RestoreFromSystemTray();
        else
        {
            ShowInTaskbar = true;
            Activate();
        }
        Close();
    }

    private static string RandomSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static void AddArgument(ProcessStartInfo start, string name, string value)
    {
        start.ArgumentList.Add(name);
        start.ArgumentList.Add(value);
    }

    private string NodeCompatiblePath(string path)
    {
        _nodePathAlias ??= WindowsAsciiPathAlias.CreateForLocalAppData();
        return _nodePathAlias.Map(path);
    }

    private void ReleaseNodePathAliasIfIdle()
    {
        if (IsNodeProcessRunning() || _gpuMinerProcess is { HasExited: false }) return;
        _nodePathAlias?.Dispose();
        _nodePathAlias = null;
    }

    private void QueueSubminerRpcLog(string message)
    {
        if (!IsDisposed && IsHandleCreated)
            BeginInvoke((Action)(() => WriteSubminerRpcLog(message)));
    }

    private void WriteSubminerRpcLog(string message, string? detail = null)
    {
        if (IsDisposed) return;
        const int maxEntries = 100;
        if (_subminerRpcLog.Count == maxEntries) _subminerRpcLog.Dequeue();
        _subminerRpcLog.Enqueue($"[{DateTime.Now:HH:mm:ss}] {TranslateUiText(message)}" +
            (detail is null ? "" : $" · {detail}"));
        if (_currentPage == "subminer") RefreshSubminerRpcLog();
    }

    private void RefreshSubminerRpcLog()
    {
        _subminerRpcLogBox.Lines = _subminerRpcLog.Count == 0
            ? new[] { TranslateUiText("Попыток подключения ещё не было.") }
            : _subminerRpcLog.ToArray();
        _subminerRpcLogBox.SelectionStart = _subminerRpcLogBox.TextLength;
        _subminerRpcLogBox.ScrollToCaret();
    }

    private void ConfirmRpcConnection(string endpoint)
    {
        const string title = "Подмайнер подключился к RPC";
        _gpuStatusText = TranslateUiText(title) + $" ({endpoint})";
        if (_currentPage == "subminer") _gpuMiningState.Text = _gpuStatusText;
        WriteNodeLog($"[GPU-подмайнер] {_gpuStatusText}.");
        if (_notifySubminerRpc) ShowSystemNotification(title, endpoint);
    }

    private void WriteNodeLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        try
        {
            lock (this)
            {
                if (_logWriter is not null) _logWriter.WriteLine(line);
                else
                {
                    Directory.CreateDirectory(AppPaths.DataRoot);
                    File.AppendAllText(AppPaths.LogPath, line + Environment.NewLine, new UTF8Encoding(false));
                }
            }
        }
        catch { }
    }

    private static long GetFileLengthOrZero(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
    }

    private static bool NodeLogContainsWalletLoadFailure(long logStartOffset)
    {
        try
        {
            using var stream = new FileStream(AppPaths.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Position = Math.Clamp(logStartOffset, 0, stream.Length);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string sessionLog = reader.ReadToEnd();
            return sessionLog.Contains("Failed to load wallet from ", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static long ReadInt64(JsonElement value, string property)
        => value.TryGetProperty(property, out JsonElement element) && element.TryGetInt64(out long number) ? number : 0;

    private static bool ReadBool(JsonElement value, string property)
        => value.TryGetProperty(property, out JsonElement element) && element.ValueKind == JsonValueKind.True;

    private static string ReadString(JsonElement value, string property)
        => value.TryGetProperty(property, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? ""
            : "";

    private static decimal ReadDecimal(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out JsonElement element)) return 0;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out decimal number)) return number;
        if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out number)) return number;
        return 0;
    }

    private string FormatGcAmount(decimal amount, bool includePositiveSign = false)
    {
        string prefix = includePositiveSign && amount > 0 ? "+" : "";
        return prefix + amount.ToString("0.########", UiLocalization.Culture(_uiLanguage)) + " GC";
    }

    private string FormatTransactionTime(JsonElement entry)
    {
        long timestamp = ReadInt64(entry, "time");
        if (timestamp <= 0) timestamp = ReadInt64(entry, "timereceived");
        if (timestamp <= 0) return "—";
        try { return DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime().ToString("g", UiLocalization.Culture(_uiLanguage)); }
        catch (ArgumentOutOfRangeException) { return "—"; }
    }

    private static double ReadDouble(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out JsonElement element)) return 0;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out double number)) return Math.Max(0, number);
        if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out number)) return Math.Max(0, number);
        return 0;
    }

    private string FormatHashrate(double value)
    {
        string[] units = { "хеш/с", "кХеш/с", "МХеш/с", "ГХеш/с", "ТХеш/с", "ПХеш/с" };
        int unit = 0;
        while (value >= 1000 && unit < units.Length - 1)
        {
            value /= 1000;
            unit++;
        }
        return value.ToString(value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00", UiLocalization.Culture(_uiLanguage)) + " " + units[unit];
    }

    private Panel AddCard(int x, int y, int width, int height)
    {
        var card = new Panel
        {
            Name = "contentCard",
            Location = UiPoint(x, y),
            Size = UiSize(width, height),
            BackColor = Surface,
            BorderStyle = _modernDesign ? BorderStyle.None : BorderStyle.FixedSingle
        };
        _pagePanel.Controls.Add(card);
        return card;
    }

    private void AddParagraph(string text, int x, int y, int width, int height)
    {
        _pagePanel.Controls.Add(new Label
        {
            Text = text,
            Location = UiPoint(x, y),
            Size = UiSize(width, height),
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10),
            AutoEllipsis = true
        });
    }

    private void AddLabel(Control parent, string text, int x, int y, int width, int height, Color color, float size, bool bold = false)
    {
        parent.Controls.Add(new Label
        {
            Text = text,
            Location = UiPoint(x, y),
            Size = UiSize(width, height),
            ForeColor = color,
            Font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            AutoEllipsis = true
        });
    }

    private void StylePrimaryButton(Button button, Point location, Size size)
    {
        _primaryButtons.Add(button);
        button.Location = UiPoint(location.X, location.Y);
        button.Size = UiSize(size.Width, size.Height);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Accent;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private void StyleSecondaryButton(Button button, Point location, Size size)
    {
        _secondaryButtons.Add(button);
        button.Location = UiPoint(location.X, location.Y);
        button.Size = UiSize(size.Width, size.Height);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Divider;
        button.FlatAppearance.BorderSize = _modernDesign ? 0 : 1;
        button.FlatAppearance.MouseOverBackColor = _modernDesign
            ? (_darkTheme ? Color.FromArgb(42, 66, 65) : Color.FromArgb(218, 238, 230))
            : (_darkTheme ? Color.FromArgb(48, 55, 50) : Color.FromArgb(239, 240, 234));
        button.BackColor = Surface;
        button.ForeColor = Ink;
        button.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private void StyleTextBox(TextBox box, Point location, Size size)
    {
        box.Location = UiPoint(location.X, location.Y);
        box.Size = UiSize(size.Width, size.Height);
        box.Font = new Font("Segoe UI", 11);
        box.BorderStyle = BorderStyle.FixedSingle;
        box.BackColor = InputSurface;
        box.ForeColor = Ink;
    }

    private void LoadUiPreferences()
    {
        try
        {
            if (!File.Exists(AppPaths.UiSettingsPath)) return;
            UiPreferences? settings = JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(AppPaths.UiSettingsPath));
            _uiLanguage = UiLocalization.NormalizeLanguage(settings?.Language);
            _darkTheme = settings?.DarkTheme ?? false;
            _modernDesign = settings?.ModernDesign ?? false;
            _firstRunGuideCompleted = settings?.FirstRunGuideCompleted ?? false;
            _firstRunBackupConfirmed = settings?.FirstRunBackupConfirmed ?? false;
            _notificationsEnabled = settings?.NotificationsEnabled ?? true;
            _notifyNodeStopped = settings?.NotifyNodeStopped ?? true;
            _notifySubminerRpc = settings?.NotifySubminerRpc ?? true;
            _notifySyncComplete = settings?.NotifySyncComplete ?? true;
            _notifyMiningRewards = settings?.NotifyMiningRewards ?? true;
            _notifyIncomingTransfers = settings?.NotifyIncomingTransfers ?? true;
            _notifyOutgoingTransfers = settings?.NotifyOutgoingTransfers ?? true;
            _autoStartEnabled = settings?.AutoStartEnabled ?? false;
            _autoLockEnabled = settings?.AutoLockEnabled ?? true;
            _autoLockMinutes = Math.Clamp(settings?.AutoLockMinutes ?? 10, 1, 120);
            _windowsHelloEnabled = settings?.WindowsHelloEnabled ?? false;
            _protectedPinVerifier = settings?.ProtectedPinVerifier;
        }
        catch
        {
            _uiLanguage = "ru";
            _darkTheme = false;
            _firstRunGuideCompleted = false;
        }
    }

    private void SaveUiPreferences()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            string json = JsonSerializer.Serialize(new UiPreferences
            {
                Language = _uiLanguage,
                DarkTheme = _darkTheme,
                ModernDesign = _modernDesign,
                FirstRunGuideCompleted = _firstRunGuideCompleted,
                FirstRunBackupConfirmed = _firstRunBackupConfirmed,
                NotificationsEnabled = _notificationsEnabled,
                NotifyNodeStopped = _notifyNodeStopped,
                NotifySubminerRpc = _notifySubminerRpc,
                NotifySyncComplete = _notifySyncComplete,
                NotifyMiningRewards = _notifyMiningRewards,
                NotifyIncomingTransfers = _notifyIncomingTransfers,
                NotifyOutgoingTransfers = _notifyOutgoingTransfers,
                AutoStartEnabled = _autoStartEnabled,
                AutoLockEnabled = _autoLockEnabled,
                AutoLockMinutes = _autoLockMinutes,
                WindowsHelloEnabled = _windowsHelloEnabled,
                ProtectedPinVerifier = _protectedPinVerifier
            });
            File.WriteAllText(AppPaths.UiSettingsPath, json, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            WriteNodeLog("Не удалось сохранить настройки интерфейса: " + ex.Message);
        }
    }

    private void UpdatePreferenceControlText()
    {
        _themeButton.Text = _darkTheme ? "Светлая тема" : "Тёмная тема";
        _designButton.Text = "Дизайн";
        _headerToolTip.SetToolTip(_designButton, TranslateUiText(_modernDesign ? "Switch to classic design" : "Switch to modern design"));
        if (_trayOpenMenuItem is not null) _trayOpenMenuItem.Text = TranslateUiText("Открыть GeoCoin-Start");
        if (_trayExitMenuItem is not null) _trayExitMenuItem.Text = TranslateUiText("Выйти из GeoCoin-Start");
        int selectedIndex = Array.IndexOf(UiLocalization.LanguageCodes, _uiLanguage);
        _languageCombo.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
    }

    private async Task CheckForUpdatesAsync()
    {
        if (!_checkUpdatesButton.Enabled) return;
        _checkUpdatesButton.Enabled = false;
        _checkUpdatesButton.Text = TranslateUiText("Проверяю обновления…");
        string? downloadedInstaller = null;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GeoCoin-Start");
            using HttpResponseMessage latestResponse = await client.GetAsync(
                AppPaths.LatestReleasePageUrl, HttpCompletionOption.ResponseHeadersRead);
            latestResponse.EnsureSuccessStatusCode();
            Uri? latestPageUri = latestResponse.RequestMessage?.RequestUri;
            string[] releasePath = latestPageUri?.AbsolutePath.Trim('/').Split('/') ?? Array.Empty<string>();
            if (latestPageUri is null || latestPageUri.Scheme != Uri.UriSchemeHttps ||
                !latestPageUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
                releasePath.Length != 5 ||
                !releasePath[0].Equals("TheTimkol", StringComparison.OrdinalIgnoreCase) ||
                !releasePath[1].Equals("geocoin-start", StringComparison.OrdinalIgnoreCase) ||
                !releasePath[2].Equals("releases", StringComparison.OrdinalIgnoreCase) ||
                !releasePath[3].Equals("tag", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Не удалось определить последний опубликованный релиз GeoCoin-Start.");

            string tag = Uri.UnescapeDataString(releasePath[4]);
            if (!TryParseReleaseVersion(tag, out Version? latestVersion) ||
                !TryParseReleaseVersion(AppPaths.AppVersion, out Version? currentVersion))
                throw new InvalidDataException("В релизе указан некорректный номер версии.");

            if (latestVersion <= currentVersion)
            {
                ShowLocalizedMessage("У вас установлена последняя версия GeoCoin-Start.", "Проверить обновления",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string releaseDownloadRoot = $"https://github.com/TheTimkol/geocoin-start/releases/download/{Uri.EscapeDataString(tag)}/";
            var setupUri = new Uri(releaseDownloadRoot + "GeoCoin-Start-Setup.exe", UriKind.Absolute);
            var checksumUri = new Uri(releaseDownloadRoot + "SHA256SUMS.txt", UriKind.Absolute);
            using HttpResponseMessage checksumResponse = await client.GetAsync(checksumUri);
            checksumResponse.EnsureSuccessStatusCode();
            string checksumText = await checksumResponse.Content.ReadAsStringAsync();
            string? checksumLine = checksumText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => line.TrimEnd().EndsWith("GeoCoin-Start-Setup.exe", StringComparison.OrdinalIgnoreCase));
            string expectedHash = checksumLine?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (!IsSha256Hex(expectedHash))
                throw new InvalidDataException("В последнем релизе нет корректной SHA-256 суммы установщика.");

            string message = TranslateUiText("Доступна версия GeoCoin-Start: ") + tag + Environment.NewLine + Environment.NewLine +
                TranslateUiText("Скачать и установить это обновление? Приложение закроется. Нода и майнер будут остановлены штатно.");
            DialogResult install = ShowLocalizedMessage(message, "Доступно обновление",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
            if (install != DialogResult.Yes) return;

            _checkUpdatesButton.Text = TranslateUiText("Скачиваю установщик обновления…");
            downloadedInstaller = Path.Combine(Path.GetTempPath(), $"GeoCoin-Start-Update-{Guid.NewGuid():N}.exe");
            using (HttpResponseMessage installerResponse = await client.GetAsync(setupUri, HttpCompletionOption.ResponseHeadersRead))
            {
                installerResponse.EnsureSuccessStatusCode();
                await using Stream source = await installerResponse.Content.ReadAsStreamAsync();
                await using var destination = new FileStream(downloadedInstaller, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(destination);
            }

            await using (FileStream installerFile = File.OpenRead(downloadedInstaller))
            {
                string actualHash = Convert.ToHexString(await SHA256.HashDataAsync(installerFile));
                if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Проверка SHA-256 загруженного установщика не пройдена.");
            }
            if (IsDisposed || !IsHandleCreated)
            {
                try { File.Delete(downloadedInstaller); } catch { }
                downloadedInstaller = null;
                return;
            }

            await UpdateSignature.VerifyAsync(Environment.ProcessPath!, downloadedInstaller);
            _updateInstallerToLaunch = downloadedInstaller;
            downloadedInstaller = null;
            // An update must exit the process so the installer can replace its files;
            // ordinary window-close behavior hides the app in the tray instead.
            _trayExitRequested = true;
            Close();
        }
        catch (Exception ex)
        {
            if (downloadedInstaller is not null)
            {
                try { File.Delete(downloadedInstaller); } catch { }
            }
            if (!IsDisposed)
                ShowLocalizedMessage("Не удалось проверить обновления: " + ex.Message, "Ошибка обновления",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed)
            {
                _checkUpdatesButton.Enabled = true;
                _checkUpdatesButton.Text = TranslateUiText("Проверить обновления");
            }
        }
    }

    private static bool TryParseReleaseVersion(string text, out Version? version)
    {
        string value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V')) value = value[1..];
        return Version.TryParse(value, out version);
    }

    private static bool IsSha256Hex(string value)
        => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static void ClearPendingUpdateInstaller(ref string? installerPath)
    {
        if (installerPath is null) return;
        try { File.Delete(installerPath); } catch { }
        installerPath = null;
    }

    private DialogResult ShowLocalizedMessage(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        => ThemedMessageDialog.Show(this, TranslateUiText(message), TranslateUiText(caption), buttons, icon,
            defaultButton, _darkTheme, TranslateUiText);

    private string TranslateUiText(string value)
    {
        string english = NormalizeUiTextToEnglish(value);
        if (_uiLanguage == "en") return english;
        if (_uiLanguage == "ru")
        {
            if (EnglishToRussian.TryGetValue(english, out string? russian)) return russian;
            if (UiLocalization.TryNormalizeEnglishToRussian(english, out russian)) return russian;
            return TranslateCommonPrefixes(english, english: false);
        }
        return UiLocalization.TranslateFromEnglish(_uiLanguage, english);
    }

    private string NormalizeUiTextToEnglish(string value)
    {
        if (RussianToEnglish.TryGetValue(value, out string? english)) return english;
        english = TranslateCommonPrefixes(value, english: true);
        if (!string.Equals(english, value, StringComparison.Ordinal)) return english;
        if (UiLocalization.TryNormalizeRussianToEnglish(value, out english)) return english;
        foreach (string language in UiLocalization.LanguageCodes.Skip(2))
            if (UiLocalization.TryNormalizeToEnglish(language, value, out english)) return english;
        if (EnglishToRussian.TryGetValue(value, out string? russian) && RussianToEnglish.TryGetValue(russian, out english))
            return english;
        return value;
    }

    private static string TranslateCommonPrefixes(string value, bool english)
    {
        (string Russian, string English)[] prefixes =
        {
            ("Блоков: ", "Blocks: "), ("Узлов сети: ", "Network peers: "), ("подключений: ", "connections: "),
            ("Скорость CPU: ", "CPU hashrate: "), ("Скорость GPU: ", "GPU hashrate: "), ("Скорость: ", "Hashrate: "),
            ("Сегодня: ", "Today: "), ("Текущая сессия: ", "Current session: "), ("Последняя сессия: ", "Last session: "),
            ("Сессия: ", "Session: "), ("Добыто блоков за всё время: ", "Blocks mined all time: "),
            ("Найдено адресов: ", "Addresses found: "), ("Найдено видеокарт: ", "GPUs found: "),
            ("Синхронизировано ", "Synced "), ("Подключаюсь к ", "Connecting to "),
            ("Ключ добавлен, но история восстановлена не полностью (", "Key imported, but address history is incomplete ("),
            ("Зрелые монеты: ", "Mature coins: "), ("Незрелые монеты: ", "Immature coins: "),
            ("Ориентировочная комиссия: ", "Estimated fee: "), ("Не удалось рассчитать комиссию: ", "Could not calculate the fee: "),
            ("Не удалось импортировать QR-код: ", "Could not import QR code: "), ("Не удалось сохранить QR-код: ", "Could not save QR code: "),
            ("Адрес награды: ", "Reward address: "), ("ID транзакции: ", "Transaction ID: "),
            ("Перевод принят нодой. Номер: ", "Transfer accepted by the node. ID: "),
            ("Не удалось загрузить адреса кошелька: ", "Could not load wallet addresses: "),
            ("Не удалось загрузить адреса: ", "Could not load addresses: "),
            ("Не удалось изменить настройку автозапуска: ", "Could not change startup setting: "),
            ("Не удалось импортировать ключ: ", "Could not import key: "), ("Не удалось экспортировать ключ: ", "Could not export key: "),
            ("Не удалось загрузить историю: ", "Could not load history: "),
            ("Не удалось подключиться: ", "Could not connect: "), ("Не удалось запустить GPU-майнинг: ", "Could not start GPU mining: "),
            ("Ошибка GPU-подмайнера: ", "GPU subminer error: "), ("Ошибка GPU: ", "GPU error: "),
            ("Баланс: ", "Balance: "), ("Добыто блоков: ", "Blocks mined: "), ("Хешрейт: ", "Hashrate: "),
            ("Состояние ноды: ", "Node status: "), ("Пиров: ", "Peers: "), ("CPU: ", "CPU: "), ("GPU: ", "GPU: "),
            ("Новый PIN должен содержать ровно ", "The new PIN must contain exactly "), ("Не удалось сохранить PIN-код: ", "Could not save PIN: "),
            ("Подождите ", "Wait "), ("Слишком много попыток. Подождите ", "Too many attempts. Wait ")
        };
        foreach ((string source, string target) in prefixes)
        {
            string from = english ? source : target;
            if (value.StartsWith(from, StringComparison.Ordinal))
            {
                string remainder = value[from.Length..];
                if (english)
                {
                    remainder = remainder.Replace(" блоков", " blocks", StringComparison.Ordinal)
                        .Replace(" блока", " blocks", StringComparison.Ordinal).Replace(" блок", remainder == "1 блок" ? " block" : " blocks", StringComparison.Ordinal)
                        .Replace(". Экспорт ключа создаёт незашифрованный текстовый файл.", ". Exporting a key creates an unencrypted text file.", StringComparison.Ordinal);
                    remainder = remainder.Replace("ПХеш/с", "PHash/s", StringComparison.Ordinal)
                        .Replace("ТХеш/с", "THash/s", StringComparison.Ordinal).Replace("ГХеш/с", "GHash/s", StringComparison.Ordinal)
                        .Replace("МХеш/с", "MHash/s", StringComparison.Ordinal).Replace("кХеш/с", "kHash/s", StringComparison.Ordinal)
                        .Replace("хеш/с", "hash/s", StringComparison.Ordinal);
                }
                else
                {
                    remainder = remainder.Replace(" blocks", " блоков", StringComparison.Ordinal)
                        .Replace(" block", " блок", StringComparison.Ordinal)
                        .Replace(". Exporting a key creates an unencrypted text file.", ". Экспорт ключа создаёт незашифрованный текстовый файл.", StringComparison.Ordinal);
                    remainder = remainder.Replace("PHash/s", "ПХеш/с", StringComparison.Ordinal)
                        .Replace("THash/s", "ТХеш/с", StringComparison.Ordinal).Replace("GHash/s", "ГХеш/с", StringComparison.Ordinal)
                        .Replace("MHash/s", "МХеш/с", StringComparison.Ordinal).Replace("kHash/s", "кХеш/с", StringComparison.Ordinal)
                        .Replace("hash/s", "хеш/с", StringComparison.Ordinal);
                }
                return (english ? target : source) + remainder;
            }
        }
        if (english)
        {
            value = value.Replace("Узлов сети: ", "Network peers: ", StringComparison.Ordinal)
                .Replace(" · подключений: ", " · connections: ", StringComparison.Ordinal);
            (string Russian, string English)[] hashUnits =
            {
                ("ПХеш/с", "PHash/s"), ("ТХеш/с", "THash/s"), ("ГХеш/с", "GHash/s"),
                ("МХеш/с", "MHash/s"), ("кХеш/с", "kHash/s"), ("хеш/с", "hash/s")
            };
            foreach ((string russian, string translated) in hashUnits)
                value = value.Replace(russian, translated, StringComparison.Ordinal);
        }
        else
        {
            value = value.Replace("Network peers: ", "Узлов сети: ", StringComparison.Ordinal)
                .Replace(" · connections: ", " · подключений: ", StringComparison.Ordinal);
            (string English, string Russian)[] hashUnits =
            {
                ("PHash/s", "ПХеш/с"), ("THash/s", "ТХеш/с"), ("GHash/s", "ГХеш/с"),
                ("MHash/s", "МХеш/с"), ("kHash/s", "кХеш/с"), ("hash/s", "хеш/с")
            };
            foreach ((string englishUnit, string translated) in hashUnits)
                value = value.Replace(englishUnit, translated, StringComparison.Ordinal);
        }
        return value;
    }

    private void ApplyUiLanguage(Control root)
    {
        if (root is TextBox textBox)
            textBox.PlaceholderText = TranslateUiText(textBox.PlaceholderText);
        else if (root is not ComboBox)
            root.Text = TranslateUiText(root.Text);

        if (root is DataGridView grid)
        {
            foreach (DataGridViewColumn column in grid.Columns)
                column.HeaderText = TranslateUiText(column.HeaderText);
            foreach (DataGridViewRow row in grid.Rows)
                foreach (DataGridViewCell cell in row.Cells)
                    if (cell.Value is string value) cell.Value = TranslateUiText(value);
        }

        foreach (Control child in root.Controls)
            ApplyUiLanguage(child);

        if (root == this)
            LayoutHeaderStatus();
    }

    private void ApplyThemeToTree(Control root)
    {
        if (root == this || root == _headerPanel || root == _pagePanel)
            root.BackColor = Canvas;
        else if (root.Name is "navigationRail" or "navigationList" or "railFooter")
            root.BackColor = Rail;
        else if (root == _receiveQrCode)
            root.BackColor = Color.White;
        else if (root is TextBox textBox)
        {
            if (textBox != _nodeLogBox)
            {
                textBox.BackColor = InputSurface;
                textBox.ForeColor = Ink;
            }
        }
        else if (root is ComboBox combo)
        {
            combo.FlatStyle = FlatStyle.Flat;
            combo.DrawMode = DrawMode.Normal;
            combo.BackColor = InputSurface;
            combo.ForeColor = Ink;
        }
        else if (root is CheckBox or RadioButton)
        {
            root.BackColor = Color.Transparent;
            root.ForeColor = Ink;
        }
        else if (root is NumericUpDown numeric)
        {
            numeric.BackColor = InputSurface;
            numeric.ForeColor = Ink;
        }
        else if (root is ListBox listBox)
        {
            listBox.BackColor = InputSurface;
            listBox.ForeColor = Ink;
        }
        else if (root is Panel card && card.Name == "contentCard")
        {
            card.BackColor = Surface;
            card.BorderStyle = _modernDesign ? BorderStyle.None : BorderStyle.FixedSingle;
        }
        else if (root is Button primaryButton && _primaryButtons.Contains(primaryButton))
        {
            primaryButton.FlatStyle = FlatStyle.Flat;
            primaryButton.FlatAppearance.BorderSize = 0;
            primaryButton.BackColor = Accent;
            primaryButton.ForeColor = Color.White;
        }
        else if (root is Button secondaryButton && _secondaryButtons.Contains(secondaryButton))
        {
            secondaryButton.FlatStyle = FlatStyle.Flat;
            secondaryButton.FlatAppearance.BorderSize = _modernDesign ? 0 : 1;
            secondaryButton.FlatAppearance.BorderColor = Divider;
            secondaryButton.FlatAppearance.MouseOverBackColor = _modernDesign
                ? (_darkTheme ? Color.FromArgb(42, 66, 65) : Color.FromArgb(218, 238, 230))
                : (_darkTheme ? Color.FromArgb(48, 55, 50) : Color.FromArgb(239, 240, 234));
            secondaryButton.BackColor = Surface;
            secondaryButton.ForeColor = Ink;
        }
        else if (root is Button button && button.Parent?.Name is "navigationRail" or "navigationList")
        {
            bool selected = string.Equals(button.Tag as string, _currentPage, StringComparison.Ordinal);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.BorderColor = Rail;
            button.FlatAppearance.MouseOverBackColor = _modernDesign
                ? (_darkTheme ? Color.FromArgb(31, 46, 46) : Color.FromArgb(210, 230, 222))
                : (_darkTheme ? Color.FromArgb(37, 48, 41) : Color.FromArgb(39, 52, 43));
            button.BackColor = selected
                ? (_modernDesign
                    ? (_darkTheme ? Color.FromArgb(35, 60, 56) : Color.FromArgb(204, 231, 221))
                    : (_darkTheme ? Color.FromArgb(48, 61, 52) : Color.FromArgb(44, 59, 48)))
                : Rail;
            button.ForeColor = selected
                ? (_modernDesign ? Accent : Color.FromArgb(241, 218, 173))
                : (_modernDesign ? Muted : Color.FromArgb(206, 215, 205));
        }
        else if (root is LinkLabel link && link.Parent?.Name == "railFooter")
        {
            link.LinkColor = _modernDesign
                ? (_darkTheme ? Color.FromArgb(137, 209, 190) : Color.FromArgb(23, 111, 96))
                : Color.FromArgb(202, 180, 132);
            link.ActiveLinkColor = _modernDesign ? Accent : Color.White;
            link.VisitedLinkColor = link.LinkColor;
        }
        else if (root is Panel panel)
        {
            Color background = panel.BackColor;
            if (background == LightCanvas || background == DarkCanvas || background == ModernLightCanvas || background == ModernDarkCanvas)
                panel.BackColor = Canvas;
            else if (background == Color.White || background == DarkSurface || background == ModernDarkSurface)
                panel.BackColor = Surface;
            else if (background == Color.FromArgb(220, 220, 214) || background == Color.FromArgb(61, 68, 62) ||
                     background == Color.FromArgb(207, 222, 216) || background == Color.FromArgb(53, 69, 68))
                panel.BackColor = Divider;
        }

        if (root is Label label)
        {
            if (label.Name == "brandName") label.ForeColor = _modernDesign ? Accent : Color.FromArgb(233, 208, 153);
            else if (label.Name == "brandSubtitle") label.ForeColor = _modernDesign ? Muted : Color.FromArgb(144, 161, 145);
            else if (label.ForeColor == LightInk || label.ForeColor == DarkInk || label.ForeColor == ModernLightInk || label.ForeColor == ModernDarkInk)
                label.ForeColor = Ink;
            else if (label.ForeColor == LightMuted || label.ForeColor == DarkMuted || label.ForeColor == ModernLightMuted || label.ForeColor == ModernDarkMuted)
                label.ForeColor = Muted;
        }
        if (root is DataGridView dataGrid)
        {
            dataGrid.BackgroundColor = Surface;
            dataGrid.DefaultCellStyle.BackColor = Surface;
            dataGrid.DefaultCellStyle.ForeColor = Ink;
            dataGrid.DefaultCellStyle.SelectionBackColor = _darkTheme ? Color.FromArgb(65, 78, 67) : Color.FromArgb(226, 233, 222);
            dataGrid.DefaultCellStyle.SelectionForeColor = Ink;
            dataGrid.ColumnHeadersDefaultCellStyle.BackColor = Canvas;
            dataGrid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
            dataGrid.GridColor = Divider;
        }
        foreach (Control child in root.Controls)
            ApplyThemeToTree(child);
    }

    private void RefreshThemeAwareHandles(Control root)
    {
        if (root is ThemeAwarePanel themePanel)
            themePanel.RecreateThemeHandle();
        else if (root is ThemeAwareFlowLayoutPanel themeFlow)
            themeFlow.RecreateThemeHandle();
        else if (root is ThemeAwareComboBox themeCombo)
            themeCombo.RecreateThemeHandle();
        else if (root is ThemeAwareCheckedListBox themeCheckedList)
            themeCheckedList.RefreshThemeDrawing();
        else if (root is ThemeAwareNumericUpDown themeNumeric)
            themeNumeric.RefreshThemeDrawing();

        foreach (Control child in root.Controls)
            RefreshThemeAwareHandles(child);
    }

    private static void OpenLink(string address)
        => Process.Start(new ProcessStartInfo { FileName = address, UseShellExecute = true });
}
