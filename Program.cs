using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace LargeFileCleaner;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new CleanerForm());
    }
}

internal sealed class CleanerForm : Form
{
    private readonly NumericUpDown minimumGb = new() { Minimum = 1, Maximum = 1000, Value = 1, Width = 80 };
    private readonly Button scanButton = new() { Text = "Үлкен файлдарды іздеу", AutoSize = true };
    private readonly Button deleteButton = new() { Text = "Таңдалғандарды жою", AutoSize = true, Enabled = false };
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
    private readonly Label status = new() { AutoSize = true, Text = "Іздеуді бастау үшін батырманы бас." };

    public CleanerForm()
    {
        Text = "Large File Cleaner — Үлкен файлдарды тазалау";
        Width = 1100; Height = 650; MinimumSize = new Size(800, 450);
        StartPosition = FormStartPosition.CenterScreen;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10), WrapContents = true };
        top.Controls.Add(new Label { Text = "Ең аз өлшемі (ГБ):", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
        top.Controls.Add(minimumGb);
        top.Controls.Add(scanButton);
        top.Controls.Add(deleteButton);
        top.Controls.Add(new Label { Text = "Жүйелік файлдар қорғалған; жою тек пайдаланушы профиліндегі файлдарға рұқсат.", AutoSize = true, Padding = new Padding(8, 7, 0, 0) });

        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Pick", HeaderText = "Таңдау", Width = 65 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size", HeaderText = "Өлшемі", Width = 110, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = "Файл жолы", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Қауіпсіздік", Width = 260, ReadOnly = true });
        Controls.Add(grid); Controls.Add(status); Controls.Add(top);

        scanButton.Click += async (_, _) => await ScanAsync();
        deleteButton.Click += (_, _) => DeleteSelected();
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
    }

    private async Task ScanAsync()
    {
        scanButton.Enabled = false; deleteButton.Enabled = false; grid.Rows.Clear(); status.Text = "Ізделуде...";
        long minBytes = (long)minimumGb.Value * 1024 * 1024 * 1024;
        try
        {
            var entries = await Task.Run(() =>
            {
                var result = new List<Entry>();
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    try
                    {
                        foreach (string path in Directory.EnumerateFiles(drive.RootDirectory.FullName, "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                var info = new FileInfo(path);
                                if (info.Length >= minBytes && !IsHiddenSystemOrReparse(info))
                                {
                                    bool eligible = IsSafeUserFile(info.FullName);
                                    result.Add(new Entry(info.FullName, info.Length, eligible));
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
                return result.OrderByDescending(e => e.Length).ToList();
            });

            foreach (var e in entries)
            {
                int i = grid.Rows.Add(false, SizeText(e.Length), e.Path, e.Eligible ? "Пайдаланушы файлы" : "Қорғалған / профильден тыс");
                grid.Rows[i].Tag = e;
                if (!e.Eligible) { grid.Rows[i].Cells[0].ReadOnly = true; grid.Rows[i].DefaultCellStyle.ForeColor = SystemColors.GrayText; }
            }
            status.Text = $"Табылғаны: {entries.Count}; жоюға жарамдысы: {entries.Count(e => e.Eligible)}.";
            deleteButton.Enabled = entries.Any(e => e.Eligible);
        }
        catch (Exception ex) { MessageBox.Show("Іздеу қатесі: " + ex.Message); status.Text = "Іздеу аяқталмады."; }
        finally { scanButton.Enabled = true; }
    }

    private void DeleteSelected()
    {
        var selected = grid.Rows.Cast<DataGridViewRow>()
            .Where(r => Convert.ToBoolean(r.Cells["Pick"].Value ?? false))
            .Select(r => r.Tag as Entry).Where(e => e is not null && e.Eligible).Cast<Entry>().ToList();

        if (selected.Count == 0) { MessageBox.Show("Алдымен жоюға болатын файлдарды таңда."); return; }
        string preview = string.Join(Environment.NewLine, selected.Take(6).Select(e => $"{SizeText(e.Length)} — {e.Path}"));
        if (selected.Count > 6) preview += Environment.NewLine + $"... және тағы {selected.Count - 6} файл";
        var answer = MessageBox.Show($"{selected.Count} файлды біржола жою керек пе?{Environment.NewLine}{Environment.NewLine}{preview}{Environment.NewLine}{Environment.NewLine}Бұл әрекетті қайтару мүмкін болмауы мүмкін.",
            "Жоюды растау", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        int deleted = 0, failed = 0;
        foreach (var e in selected)
        {
            try
            {
                if (!IsSafeUserFile(e.Path) || !File.Exists(e.Path)) { failed++; continue; }
                File.Delete(e.Path); deleted++;
                var row = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Tag is Entry item && item.Path == e.Path);
                if (row is not null) grid.Rows.Remove(row);
            }
            catch { failed++; }
        }
        status.Text = $"Жойылды: {deleted}; жойылмағаны: {failed}.";
        MessageBox.Show($"Жойылды: {deleted}{Environment.NewLine}Жойылмағаны: {failed}", "Нәтиже");
    }

    private static bool IsHiddenSystemOrReparse(FileInfo f) =>
        (f.Attributes & (FileAttributes.ReparsePoint | FileAttributes.System | FileAttributes.Hidden)) != 0;

    private static bool IsSafeUserFile(string path)
    {
        try
        {
            string profile = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(profile, StringComparison.OrdinalIgnoreCase)) return false;
            string relative = full[profile.Length..];
            string[] blocked = { "AppData", "Windows", "Program Files", "Program Files (x86)", "ProgramData", "$Recycle.Bin", "OneDrive" };
            if (relative.Split(Path.DirectorySeparatorChar).Any(part => blocked.Contains(part, StringComparer.OrdinalIgnoreCase))) return false;
            var info = new FileInfo(full);
            return info.Exists && !IsHiddenSystemOrReparse(info);
        }
        catch { return false; }
    }

    private static string SizeText(long bytes)
    {
        string[] units = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
        double n = bytes; int i = 0;
        while (n >= 1024 && i < units.Length - 1) { n /= 1024; i++; }
        return $"{n:0.##} {units[i]}";
    }

    private sealed record Entry(string Path, long Length, bool Eligible);
}
