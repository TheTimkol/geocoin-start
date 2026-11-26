namespace GeoCoinStart;

internal sealed record WalletPasswordChange(string? CurrentPassword, string NewPassword);

internal sealed class WalletPasswordChangeDialog : Form
{
    private readonly bool _walletIsEncrypted;
    private readonly bool _darkTheme;
    private readonly Func<string, string> _translate;
    private readonly TextBox? _currentPassword;
    private readonly TextBox _newPassword;
    private readonly TextBox _confirmPassword;

    private WalletPasswordChangeDialog(bool walletIsEncrypted, bool darkTheme, Func<string, string> translate)
    {
        _walletIsEncrypted = walletIsEncrypted;
        _darkTheme = darkTheme;
        _translate = translate;

        Color canvas = darkTheme ? Color.FromArgb(28, 35, 31) : Color.FromArgb(243, 243, 237);
        Color surface = darkTheme ? Color.FromArgb(36, 44, 39) : Color.White;
        Color ink = darkTheme ? Color.FromArgb(235, 238, 231) : Color.FromArgb(26, 39, 31);
        Color muted = darkTheme ? Color.FromArgb(171, 185, 174) : Color.FromArgb(93, 111, 96);
        Color border = darkTheme ? Color.FromArgb(77, 88, 79) : Color.FromArgb(195, 199, 189);
        Color accent = darkTheme ? Color.FromArgb(184, 147, 79) : Color.FromArgb(166, 129, 63);
        Color secondary = darkTheme ? Color.FromArgb(43, 53, 47) : Color.FromArgb(248, 248, 244);

        Text = Translate(walletIsEncrypted ? "Change wallet password" : "Set wallet password");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(500, walletIsEncrypted ? 370 : 318);
        BackColor = canvas;
        ForeColor = ink;
        Font = new Font("Segoe UI", 10f);

        Controls.Add(new Label
        {
            Text = Translate(walletIsEncrypted
                ? "Enter the current wallet password, then choose a new password."
                : "Choose a password to encrypt this wallet. The node will restart after encryption."),
            AutoSize = false,
            Location = new Point(26, 18),
            Size = new Size(448, 48),
            ForeColor = muted
        });

        int y = 78;
        if (walletIsEncrypted)
        {
            AddPasswordField(Translate("Current wallet password"), y, out TextBox current);
            _currentPassword = current;
            y += 70;
        }

        AddPasswordField(Translate("New wallet password"), y, out TextBox newPassword);
        _newPassword = newPassword;
        y += 70;
        AddPasswordField(Translate("Confirm new wallet password"), y, out TextBox confirmPassword);
        _confirmPassword = confirmPassword;

        var cancel = CreateButton(Translate("Cancel"), secondary, ink, border);
        cancel.DialogResult = DialogResult.Cancel;
        cancel.Location = new Point(262, ClientSize.Height - 54);
        Controls.Add(cancel);

        var accept = CreateButton(Translate(walletIsEncrypted ? "Change password" : "Set password"), accent,
            darkTheme ? Color.FromArgb(22, 28, 24) : Color.White, accent);
        accept.Location = new Point(372, ClientSize.Height - 54);
        accept.Click += (_, _) => AcceptPasswordChange();
        Controls.Add(accept);

        CancelButton = cancel;
        AcceptButton = accept;

        void AddPasswordField(string labelText, int top, out TextBox textBox)
        {
            Controls.Add(new Label
            {
                Text = labelText,
                AutoSize = true,
                Location = new Point(28, top),
                ForeColor = ink
            });
            textBox = new TextBox
            {
                Location = new Point(28, top + 24),
                Size = new Size(444, 32),
                UseSystemPasswordChar = true,
                BackColor = darkTheme ? Color.FromArgb(38, 48, 42) : Color.FromArgb(250, 250, 247),
                ForeColor = ink,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(textBox);
        }

        Button CreateButton(string text, Color back, Color fore, Color outline)
        {
            var button = new Button
            {
                Text = text,
                Size = new Size(100, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = fore
            };
            button.FlatAppearance.BorderColor = outline;
            button.FlatAppearance.MouseOverBackColor = darkTheme
                ? Color.FromArgb(58, 68, 60)
                : Color.FromArgb(231, 232, 226);
            return button;
        }
    }

    public static WalletPasswordChange? Ask(IWin32Window owner, bool walletIsEncrypted, bool darkTheme,
        Func<string, string> translate)
    {
        using var dialog = new WalletPasswordChangeDialog(walletIsEncrypted, darkTheme, translate);
        if (dialog.ShowDialog(owner) != DialogResult.OK) return null;

        string? currentPassword = dialog._currentPassword?.Text;
        string newPassword = dialog._newPassword.Text;
        dialog.ClearPasswordFields();
        return new WalletPasswordChange(currentPassword, newPassword);
    }

    private string Translate(string value) => _translate(value);

    private void AcceptPasswordChange()
    {
        if (_walletIsEncrypted && string.IsNullOrEmpty(_currentPassword?.Text))
        {
            ShowValidationError("Enter the current wallet password.");
            return;
        }

        if (string.IsNullOrEmpty(_newPassword.Text))
        {
            ShowValidationError("The wallet password cannot be empty.");
            return;
        }

        if (_newPassword.Text != _confirmPassword.Text)
        {
            ShowValidationError("The passwords do not match.");
            return;
        }

        if (_walletIsEncrypted && string.Equals(_currentPassword?.Text, _newPassword.Text, StringComparison.Ordinal))
        {
            ShowValidationError("Choose a password different from the current one.");
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowValidationError(string message)
        => ThemedMessageDialog.Show(this, Translate(message), Translate("Wallet password"), MessageBoxButtons.OK,
            MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, _darkTheme, _translate);

    private void ClearPasswordFields()
    {
        if (_currentPassword is not null) _currentPassword.Clear();
        _newPassword.Clear();
        _confirmPassword.Clear();
    }
}
