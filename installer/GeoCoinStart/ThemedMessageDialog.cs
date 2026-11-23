namespace GeoCoinStart;

internal static class ThemedMessageDialog
{
    public static DialogResult Show(
        IWin32Window owner,
        string message,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        bool darkTheme,
        Func<string, string> translate)
    {
        Color canvas = darkTheme ? Color.FromArgb(28, 35, 31) : Color.FromArgb(243, 243, 237);
        Color surface = darkTheme ? Color.FromArgb(36, 44, 39) : Color.White;
        Color ink = darkTheme ? Color.FromArgb(235, 238, 231) : Color.FromArgb(26, 39, 31);
        Color muted = darkTheme ? Color.FromArgb(171, 185, 174) : Color.FromArgb(93, 111, 96);
        Color border = darkTheme ? Color.FromArgb(77, 88, 79) : Color.FromArgb(195, 199, 189);
        Color accent = darkTheme ? Color.FromArgb(184, 147, 79) : Color.FromArgb(166, 129, 63);
        Color secondary = darkTheme ? Color.FromArgb(43, 53, 47) : Color.FromArgb(248, 248, 244);

        const int dialogWidth = 540;
        const int headerHeight = 42;
        const int footerHeight = 62;
        const int bodyWidth = dialogWidth - 112;
        using var font = new Font("Segoe UI", 10f);
        Size measured = TextRenderer.MeasureText(message, font, new Size(bodyWidth, 2000),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
        int desiredBodyHeight = Math.Max(82, measured.Height + 40);
        int maxHeight = Math.Max(240, Screen.FromHandle(owner.Handle).WorkingArea.Height - 64);
        int dialogHeight = Math.Min(maxHeight, headerHeight + footerHeight + desiredBodyHeight);
        int bodyHeight = dialogHeight - headerHeight - footerHeight;

        using var dialog = new Form
        {
            Text = caption,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            ShowInTaskbar = false,
            MinimizeBox = false,
            MaximizeBox = false,
            KeyPreview = true,
            ClientSize = new Size(dialogWidth, dialogHeight),
            BackColor = canvas,
            ForeColor = ink,
            Font = font,
            AutoScaleMode = AutoScaleMode.Dpi
        };
        void CenterDialogOnOwnerOrScreen()
        {
            Rectangle workingArea = Screen.FromHandle(owner.Handle).WorkingArea;
            Rectangle anchor = owner is Form ownerForm && ownerForm.Visible && ownerForm.WindowState != FormWindowState.Minimized
                ? ownerForm.Bounds
                : workingArea;
            int left = anchor.Left + (anchor.Width - dialog.Width) / 2;
            int top = anchor.Top + (anchor.Height - dialog.Height) / 2;
            dialog.Location = new Point(
                Math.Clamp(left, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - dialog.Width)),
                Math.Clamp(top, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - dialog.Height)));
        }
        CenterDialogOnOwnerOrScreen();
        dialog.Load += (_, _) => CenterDialogOnOwnerOrScreen();
        dialog.Paint += (_, e) =>
        {
            using var pen = new Pen(border);
            e.Graphics.DrawRectangle(pen, 0, 0, dialog.ClientSize.Width - 1, dialog.ClientSize.Height - 1);
        };

        var header = new Panel { Location = Point.Empty, Size = new Size(dialogWidth, headerHeight), BackColor = canvas };
        var title = new Label
        {
            Text = caption,
            Location = new Point(16, 0),
            Size = new Size(dialogWidth - 68, headerHeight),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = ink,
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
        };
        bool closeHovered = false;
        var close = new Label
        {
            Text = string.Empty,
            Location = new Point(dialogWidth - 41, 0),
            Size = new Size(40, headerHeight),
            BackColor = canvas,
            ForeColor = muted,
            Cursor = Cursors.Hand
        };
        close.Paint += (_, e) =>
        {
            if (closeHovered)
            {
                using var brush = new SolidBrush(darkTheme ? Color.FromArgb(58, 65, 60) : Color.FromArgb(231, 232, 226));
                e.Graphics.FillRectangle(brush, close.ClientRectangle);
            }
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float centerX = close.ClientSize.Width / 2f;
            float centerY = close.ClientSize.Height / 2f - 4f;
            using var pen = new Pen(muted, 1.5f);
            pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            e.Graphics.DrawLine(pen, centerX - 5, centerY - 5, centerX + 5, centerY + 5);
            e.Graphics.DrawLine(pen, centerX + 5, centerY - 5, centerX - 5, centerY + 5);
        };
        close.MouseEnter += (_, _) => { closeHovered = true; close.Invalidate(); };
        close.MouseLeave += (_, _) => { closeHovered = false; close.Invalidate(); };
        close.Click += (_, _) => dialog.Close();
        header.Controls.Add(title);
        header.Controls.Add(close);
        dialog.Controls.Add(header);

        var body = new Panel
        {
            Location = new Point(1, headerHeight),
            Size = new Size(dialogWidth - 2, bodyHeight),
            BackColor = surface,
            AutoScroll = measured.Height + 36 > bodyHeight
        };
        bool errorIcon = icon is MessageBoxIcon.Error or MessageBoxIcon.Hand or MessageBoxIcon.Stop;
        bool warningIcon = icon is MessageBoxIcon.Warning or MessageBoxIcon.Exclamation;
        string glyphText = errorIcon ? "×" : warningIcon ? "!" : icon == MessageBoxIcon.Question ? "?" : "i";
        Color glyphColor = errorIcon ? Color.FromArgb(177, 75, 59) : warningIcon ? accent :
            darkTheme ? Color.FromArgb(66, 123, 143) : Color.FromArgb(63, 117, 136);
        int contentHeight = Math.Max(1, Math.Max(bodyHeight - 36, measured.Height));
        using var glyphFont = new Font("Segoe UI Semibold", 16f, FontStyle.Bold);
        var glyph = new Label
        {
            Text = string.Empty,
            Location = new Point(24, 18 + Math.Max(0, (Math.Min(contentHeight, bodyHeight - 36) - 32) / 2)),
            Size = new Size(32, 32),
            BackColor = surface,
            ForeColor = Color.White
        };
        glyph.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(glyphColor);
            e.Graphics.FillEllipse(brush, 0, 0, glyph.Width - 1, glyph.Height - 1);
            TextRenderer.DrawText(e.Graphics, glyphText, glyphFont, glyph.ClientRectangle, glyph.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        };
        var content = new Label
        {
            Text = message,
            Location = new Point(72, 18),
            Size = new Size(bodyWidth, contentHeight),
            AutoSize = false,
            ForeColor = ink,
            BackColor = surface,
            Font = font,
            UseMnemonic = false
        };
        body.Controls.Add(glyph);
        body.Controls.Add(content);
        dialog.Controls.Add(body);

        var footer = new Panel
        {
            Location = new Point(1, dialogHeight - footerHeight - 1),
            Size = new Size(dialogWidth - 2, footerHeight),
            BackColor = canvas
        };
        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 12, 16, 8),
            BackColor = canvas
        };
        var options = GetButtons(buttons, translate);
        var dialogButtons = new List<Button>();
        foreach ((string text, DialogResult result, bool primary) in options)
        {
            var button = new Button
            {
                Text = text,
                DialogResult = result,
                Size = new Size(104, 36),
                Margin = new Padding(0, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? accent : secondary,
                ForeColor = primary ? (darkTheme ? Color.FromArgb(22, 28, 24) : Color.White) : ink,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold)
            };
            button.FlatAppearance.BorderColor = border;
            button.FlatAppearance.MouseOverBackColor = primary
                ? (darkTheme ? Color.FromArgb(203, 165, 96) : Color.FromArgb(180, 140, 69))
                : (darkTheme ? Color.FromArgb(55, 65, 58) : Color.FromArgb(237, 238, 232));
            buttonsPanel.Controls.Add(button);
            dialogButtons.Add(button);
        }
        footer.Controls.Add(buttonsPanel);
        dialog.Controls.Add(footer);

        DialogResult closeResult = options.FirstOrDefault(option => option.Result == DialogResult.Cancel).Result;
        if (closeResult == DialogResult.None)
            closeResult = options.FirstOrDefault(option => option.Result == DialogResult.No).Result;
        if (closeResult == DialogResult.None)
            closeResult = options.FirstOrDefault().Result;
        dialog.FormClosing += (_, e) =>
        {
            if (dialog.DialogResult == DialogResult.None)
                dialog.DialogResult = closeResult;
        };
        int defaultIndex = Math.Clamp((int)defaultButton - 1, 0, dialogButtons.Count - 1);
        dialog.AcceptButton = dialogButtons[defaultIndex];
        dialog.CancelButton = dialogButtons.FirstOrDefault(button => button.DialogResult == DialogResult.Cancel)
            ?? dialogButtons.FirstOrDefault(button => button.DialogResult == DialogResult.No)
            ?? dialogButtons.FirstOrDefault();
        dialog.Shown += (_, _) => dialogButtons[defaultIndex].Focus();

        return dialog.ShowDialog(owner);
    }

    private static List<(string Text, DialogResult Result, bool Primary)> GetButtons(MessageBoxButtons buttons, Func<string, string> translate)
    {
        string T(string key) => translate(key);
        return buttons switch
        {
            MessageBoxButtons.OKCancel => new() { (T("OK"), DialogResult.OK, true), (T("Cancel"), DialogResult.Cancel, false) },
            MessageBoxButtons.YesNo => new() { (T("Yes"), DialogResult.Yes, true), (T("No"), DialogResult.No, false) },
            MessageBoxButtons.YesNoCancel => new() { (T("Yes"), DialogResult.Yes, true), (T("No"), DialogResult.No, false), (T("Cancel"), DialogResult.Cancel, false) },
            MessageBoxButtons.RetryCancel => new() { (T("Retry"), DialogResult.Retry, true), (T("Cancel"), DialogResult.Cancel, false) },
            _ => new() { (T("OK"), DialogResult.OK, true) }
        };
    }
}
