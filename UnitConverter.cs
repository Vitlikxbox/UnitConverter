using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

class UnitConverter : Form
{
    class Unit
    {
        public string Name;
        public Func<double, double> ToBase, FromBase;
        public Unit(string name, double factor) { Name = name; ToBase = v => v * factor; FromBase = v => v / factor; }
        public Unit(string name, Func<double, double> toBase, Func<double, double> fromBase) { Name = name; ToBase = toBase; FromBase = fromBase; }
    }

    readonly Dictionary<string, Unit[]> categories = new Dictionary<string, Unit[]>
    {
        { "📏 Длина", new[] {
            new Unit("миллиметр", 0.001), new Unit("сантиметр", 0.01), new Unit("метр", 1), new Unit("километр", 1000),
            new Unit("дюйм", 0.0254), new Unit("фут", 0.3048), new Unit("ярд", 0.9144), new Unit("миля", 1609.344), new Unit("морская миля", 1852) } },
        { "⚖️ Масса", new[] {
            new Unit("миллиграмм", 1e-6), new Unit("грамм", 0.001), new Unit("килограмм", 1), new Unit("центнер", 100), new Unit("тонна", 1000),
            new Unit("унция", 0.028349523125), new Unit("фунт", 0.45359237), new Unit("карат", 0.0002) } },
        { "🌡️ Температура", new[] {
            new Unit("°C Цельсий", v => v, v => v),
            new Unit("°F Фаренгейт", v => (v - 32) * 5 / 9, v => v * 9 / 5 + 32),
            new Unit("K Кельвин", v => v - 273.15, v => v + 273.15) } },
        { "🧪 Объём", new[] {
            new Unit("миллилитр", 0.001), new Unit("литр", 1), new Unit("кубометр", 1000), new Unit("чайная ложка", 0.005), new Unit("столовая ложка", 0.015),
            new Unit("стакан (250 мл)", 0.25), new Unit("галлон США", 3.785411784), new Unit("пинта США", 0.473176473), new Unit("жидкая унция США", 0.0295735295625) } },
        { "⬛ Площадь", new[] {
            new Unit("кв. сантиметр", 1e-4), new Unit("кв. метр", 1), new Unit("сотка", 100), new Unit("гектар", 10000), new Unit("кв. километр", 1e6),
            new Unit("кв. фут", 0.09290304), new Unit("акр", 4046.8564224) } },
        { "🚗 Скорость", new[] {
            new Unit("м/с", 1), new Unit("км/ч", 1 / 3.6), new Unit("миль/ч", 0.44704), new Unit("узел", 1852 / 3600.0), new Unit("мах (у земли)", 340.3) } },
        { "💾 Данные", new[] {
            new Unit("бит", 0.125), new Unit("байт", 1), new Unit("килобайт (КБ)", 1024), new Unit("мегабайт (МБ)", 1048576), new Unit("гигабайт (ГБ)", 1073741824),
            new Unit("терабайт (ТБ)", 1099511627776), new Unit("мегабит", 125000), new Unit("гигабит", 125000000) } },
        { "⏱️ Время", new[] {
            new Unit("миллисекунда", 0.001), new Unit("секунда", 1), new Unit("минута", 60), new Unit("час", 3600), new Unit("сутки", 86400),
            new Unit("неделя", 604800), new Unit("год (365 дн.)", 31536000) } },
        { "⚡ Энергия", new[] {
            new Unit("джоуль", 1), new Unit("килоджоуль", 1000), new Unit("калория", 4.184), new Unit("килокалория", 4184), new Unit("ватт-час", 3600), new Unit("киловатт-час", 3600000) } },
        { "🔧 Давление", new[] {
            new Unit("паскаль", 1), new Unit("килопаскаль", 1000), new Unit("бар", 100000), new Unit("атмосфера", 101325), new Unit("мм рт. ст.", 133.322387415), new Unit("psi", 6894.757293168) } },
    };

    readonly Font boldFont = new Font("Segoe UI", 10, FontStyle.Bold);
    ListBox categoryList;
    TextBox valueBox;
    ComboBox fromBox;
    ListView results;
    Label hint;

    UnitConverter()
    {
        Text = "UnitConverter";
        ClientSize = new Size(640, 420);
        MinimumSize = new Size(560, 360);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;

        categoryList = new ListBox { Location = new Point(12, 12), Size = new Size(170, 396), IntegralHeight = false, ItemHeight = 30, DrawMode = DrawMode.OwnerDrawFixed, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left };
        categoryList.Items.AddRange(categories.Keys.ToArray());
        categoryList.DrawItem += DrawCategory;
        categoryList.SelectedIndexChanged += (s, e) => LoadCategory();
        Controls.Add(categoryList);

        valueBox = new TextBox { Location = new Point(194, 12), Width = 180, Font = new Font("Segoe UI", 14), Text = "1" };
        valueBox.TextChanged += (s, e) => Convert();
        Controls.Add(valueBox);
        fromBox = new ComboBox { Location = new Point(384, 16), Width = 244, DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        fromBox.SelectedIndexChanged += (s, e) => Convert();
        Controls.Add(fromBox);

        results = new ListView { Location = new Point(194, 58), Size = new Size(434, 318), View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.None, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        results.Columns.Add("", 0); // the first column cannot be right-aligned
        results.Columns.Add("", 210, HorizontalAlignment.Right);
        results.Columns.Add("", 200);
        results.DoubleClick += (s, e) => Copy();
        results.KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.C) Copy(); };
        results.Resize += (s, e) => { results.Columns[1].Width = results.ClientSize.Width / 2; results.Columns[2].Width = results.ClientSize.Width - results.Columns[1].Width - 4; };
        Controls.Add(results);

        hint = new Label { Location = new Point(194, 384), AutoSize = true, ForeColor = Color.Gray, Text = "Двойной клик по строке копирует число", Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        Controls.Add(hint);

        categoryList.SelectedIndex = 0;
    }

    void DrawCategory(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var back = new SolidBrush(selected ? Color.FromArgb(220, 232, 248) : SystemColors.Window)) e.Graphics.FillRectangle(back, e.Bounds);
        TextRenderer.DrawText(e.Graphics, (string)categoryList.Items[e.Index], selected ? boldFont : categoryList.Font,
            new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), SystemColors.WindowText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }

    Unit[] Current()
    {
        return categories[(string)categoryList.SelectedItem];
    }

    void LoadCategory()
    {
        fromBox.Items.Clear();
        foreach (var u in Current()) fromBox.Items.Add(u.Name);
        int baseIndex = Array.FindIndex(Current(), u => u.ToBase(1) == 1);
        fromBox.SelectedIndex = Math.Max(0, baseIndex);
        valueBox.Focus();
        valueBox.SelectAll();
    }

    void Convert()
    {
        if (fromBox.SelectedIndex < 0) return;
        results.BeginUpdate();
        results.Items.Clear();
        double value;
        string text = valueBox.Text.Trim().Replace(" ", "").Replace(',', '.');
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            results.EndUpdate();
            valueBox.BackColor = text.Length == 0 ? SystemColors.Window : Color.MistyRose;
            return;
        }
        valueBox.BackColor = SystemColors.Window;
        var units = Current();
        double baseValue = units[fromBox.SelectedIndex].ToBase(value);
        for (int i = 0; i < units.Length; i++)
        {
            double v = units[i].FromBase(baseValue);
            var item = new ListViewItem(new[] { "", Format(v), "  " + units[i].Name }) { Tag = v };
            if (i == fromBox.SelectedIndex) { item.ForeColor = Color.Gray; }
            else item.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            results.Items.Add(item);
        }
        results.EndUpdate();
    }

    static string Format(double v)
    {
        if (v == 0) return "0";
        double abs = Math.Abs(v);
        if (abs >= 1e15 || abs < 1e-6) return v.ToString("0.######E+0", CultureInfo.CurrentCulture);
        var nfi = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        nfi.NumberGroupSeparator = " ";
        int digits = abs >= 1000 ? 2 : abs >= 1 ? 4 : 6;
        return Math.Round(v, digits).ToString("#,0.######", nfi);
    }

    void Copy()
    {
        if (results.SelectedItems.Count == 0) return;
        Clipboard.SetText(results.SelectedItems[0].SubItems[1].Text.Replace(" ", ""));
        hint.Text = "Скопировано: " + results.SelectedItems[0].SubItems[1].Text + results.SelectedItems[0].SubItems[2].Text;
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new UnitConverter());
    }
}
