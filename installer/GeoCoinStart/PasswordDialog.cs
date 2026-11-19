namespace GeoCoinStart;

internal sealed class PasswordDialog : Form
{
    private readonly TextBox _password;
    private readonly TextBox? _confirmation;

    private PasswordDialog(string title, string message, bool create)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(470, create ? 300 : 240);
        BackColor = Color.FromArgb(24, 32, 28);
        ForeColor = Color.FromArgb(237, 239, 233);
        Font = new Font("Segoe UI", 10);

        Controls.Add(new Label
        {
            Text = message,
            AutoSize = false,
            Location = new Point(26, 24),
            Size = new Size(418, create ? 68 : 54),
            ForeColor = Color.FromArgb(196, 205, 195)
        });

        int top = create ? 108 : 100;
        Controls.Add(new Label { Text = "Пароль кошелька", AutoSize = true, Location = new Point(28, top) });
        _password = new TextBox
        {
            Location = new Point(28, top + 26),
            Size = new Size(414, 30),
            UseSystemPasswordChar = true,
            BackColor = Color.FromArgb(38, 48, 42),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_password);

        if (create)
        {
            Controls.Add(new Label { Text = "Повторите пароль", AutoSize = true, Location = new Point(28, 170) });
            _confirmation = new TextBox
            {
                Location = new Point(28, 196),
                Size = new Size(414, 30),
                UseSystemPasswordChar = true,
                BackColor = Color.FromArgb(38, 48, 42),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_confirmation);
        }

        var cancel = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            Location = new Point(230, create ? 246 : 190),
            Size = new Size(100, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(48, 59, 51),
            ForeColor = Color.White
        };
        Controls.Add(cancel);

        var accept = new Button
        {
            Text = create ? "Создать" : "Продолжить",
            Location = new Point(342, create ? 246 : 190),
            Size = new Size(100, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(182, 149, 86),
            ForeColor = Color.FromArgb(20, 27, 24)
        };
        accept.Click += (_, _) =>
        {
            if (_password.Text.Length < 10)
            {
                MessageBox.Show(this, "Задайте пароль длиной не менее 10 символов.", "Защита кошелька",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_confirmation is not null && _confirmation.Text != _password.Text)
            {
                MessageBox.Show(this, "Пароли не совпадают.", "Защита кошелька",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(accept);
        AcceptButton = accept;
        CancelButton = cancel;
    }

    public string Password => _password.Text;

    public static string? AskToCreate(IWin32Window owner)
    {
        using var dialog = new PasswordDialog(
            "Создание кошелька",
            "Придумайте пароль и сохраните его отдельно. Без него открыть кошелёк и отправить монеты не получится.",
            create: true);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Password : null;
    }

    public static string? AskToUnlock(IWin32Window owner)
    {
        using var dialog = new PasswordDialog(
            "Открытие кошелька",
            "Введите пароль, который вы задали при создании кошелька.",
            create: false);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Password : null;
    }

    public static string? AskForAction(IWin32Window owner, string action)
    {
        using var dialog = new PasswordDialog(
            "Подтвердите действие",
            $"Введите пароль кошелька, чтобы {action}.",
            create: false);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Password : null;
    }
}
