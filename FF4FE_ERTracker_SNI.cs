using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grpc.Net.Client;
using SNI;

// ──────────────────────────────────────────────────────────────────────────────
// Entry point
// ──────────────────────────────────────────────────────────────────────────────
namespace FF4FE_ERTracker_SNI
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Required for plain (non-TLS) HTTP/2 on .NET 6
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ERTrackerForm());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Entrance coordinate table
    // ─────────────────────────────────────────────────────────────────────────
    internal static class EntranceTable
    {
        public static readonly Dictionary<string, string> Map =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ── Overworld ────────────────────────────────────────────────────
            { "OW(101,157)",  "Baron"                },
            { "OW(101,156)",  "Baron"                },
            { "OW(102,157)",  "Baron"                },
            { "OW(102,156)",  "Baron"                },
            { "OW(103,158)",  "Town of Baron"        },
            { "OW(103,157)",  "Town of Baron"        },
            { "OW(100,157)",  "Town of Baron"        },
            { "OW(100,158)",  "Town of Baron"        },
            { "OW(104,215)",  "Agart"                },
            { "OW(119,58)",   "Damcyan"              },
            { "OW(125,104)",  "Kaipo"                },
            { "OW(125,66)",   "Lake"                 },
            { "OW(134,72)",   "Waterfalls"           },
            { "OW(136,56)",   "Antlion"              },
            { "OW(138,77)",   "Watery Pass North"    },
            { "OW(138,83)",   "Watery Pass South"    },
            { "OW(152,49)",   "Mt.Hobs West"         },
            { "OW(154,199)",  "Mysidia"              },
            { "OW(155,199)",  "Mysidia"              },
            { "OW(160,49)",   "Mt.Hobs East"         },
            { "OW(210,130)",  "Silvera"              },
            { "OW(213,209)",  "Ordeal's Forest"      },
            { "OW(215,58)",   "Fabul"                },
            { "OW(218,199)",  "Mt.Ordeals"           },
            { "OW(219,136)",  "Grotto Adamant"       },
            { "OW(228,47)",   "Fabul Forest"         },
            { "OW(24,231)",   "Cave Eblan"           },
            { "OW(34,101)",   "Toroian Forest"       },
            { "OW(35,81)",    "Toroian Castle"       },
            { "OW(35,82)",    "Toroian Castle"       },
            { "OW(35,83)",    "Town of Toroia"       },
            { "OW(36,84)",    "Town of Toroia"       },
            { "OW(41,53)",    "Chocobo's Village"    },
            { "OW(45,236)",   "Eblan"                },
            { "OW(74,53)",    "Cave Magnes"          },
            { "OW(76,132)",   "Misty Cave South"     },
            { "OW(84,119)",   "Misty Cave North"     },
            { "OW(86,79)",    "Center Forest"        },
            { "OW(89,163)",   "Baron Forest"         },
            { "OW(96,119)",   "Village Mist Left"    },
            { "OW(97,119)",   "Village Mist Right"   },

            // ── Underworld ───────────────────────────────────────────────────
            { "UW(100,82)",   "Castle of Dwarves"    },
            { "UW(104,123)",  "Kokkol the Smith's"   },
            { "UW(13,14)",    "Sylvan Cave"          },
            { "UW(27,86)",    "Land of Monsters"     },
            { "UW(46,109)",   "Sealed Cave"          },
            { "UW(48,16)",    "Tower of Bab-il"      },
            { "UW(62,121)",   "Tomra"                },
            { "UW(91,83)",    "Dwarf Base"           },

            // ── Moon ─────────────────────────────────────────────────────────
            { "Moon(18,14)",  "Lunar Path"           },
            { "Moon(18,20)",  "Lunar Path"           },
            { "Moon(28,25)",  "Lunar's Lair"         },
            { "Moon(40,28)",  "Lunar Path"           },
            { "Moon(41,24)",  "Lunar Path"           },
            { "Moon(61,23)",  "Cave Bahamut"         },
        };

        public static string? Lookup(string plane, int x, int y)
        {
            string key = $"{plane}({x},{y})";
            return Map.TryGetValue(key, out string? name) ? name : null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FF4 character decoding
    // ─────────────────────────────────────────────────────────────────────────
    internal static class FF4Encoding
    {
        private static readonly Dictionary<byte, char> CharMap = BuildMap();

        private static Dictionary<byte, char> BuildMap()
        {
            var m = new Dictionary<byte, char>();
            for (int i = 0x42; i <= 0x5B; i++) m[(byte)i] = (char)(i - 1);
            for (int i = 0x5C; i <= 0x75; i++) m[(byte)i] = (char)(i + 5);
            for (int i = 0x80; i <= 0x89; i++) m[(byte)i] = (char)(i - 0x80 + 0x30);
            m[0xC0] = '\''; m[0xC1] = '.'; m[0xC2] = ' ';
            m[0xC8] = ',';  m[0xC9] = '!'; m[0x76] = ' ';
            return m;
        }

        public static string? ReadNameBanner(Func<long, byte> read, long baseAddr)
        {
            var sb = new StringBuilder();
            bool hasPad = false, any = false;
            for (int i = 0; i < 32; i++)
            {
                byte b = read(baseAddr + i);
                if (b == 0xFF)
                {
                    hasPad = true;
                    if (any && (sb.Length == 0 || sb[sb.Length - 1] != ' '))
                        sb.Append(' ');
                }
                else if (b == 0x00) { }
                else if (CharMap.TryGetValue(b, out char ch)) { sb.Append(ch); any = true; }
                else return null;
            }
            if (!hasPad || !any) return null;
            string s = sb.ToString().Trim();
            return (s.Length == 0 || s.Length > 24) ? null : s;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pending transition (waiting for destination name banner)
    // ─────────────────────────────────────────────────────────────────────────
    internal class Pending
    {
        public string  FromLabel   = "";
        public int     DestMap;
        public int     DestPlane;
        public int     TicksWaited;
        // At ~60 ticks/sec these give ≈5 s and ≈10 s respectively —
        // identical feel to the BizHawk frame-based version.
        public const int NameWindow = 300;
        public const int Timeout    = 600;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Multi-column connection display
    // ─────────────────────────────────────────────────────────────────────────
    internal sealed class ConnectionPanel : Panel
    {
        private readonly List<string> _items    = new List<string>();
        private readonly Font         _font      = new Font("Consolas", 9.5f);
        private readonly Brush        _fgBrush   = new SolidBrush(Color.FromArgb(220, 220, 235));
        private readonly Brush        _arrowBrush= new SolidBrush(Color.FromArgb(100, 180, 255));
        private const int ROW_H   = 20;
        private const int COL_GAP = 20;

        public int ItemCount => _items.Count;

        public ConnectionPanel()
        {
            DoubleBuffered = true;
            BackColor      = Color.FromArgb(18, 18, 24);
        }

        public void AddItem(string text) { _items.Add(text); Invalidate(); }
        public new void Clear()          { _items.Clear();   Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_items.Count == 0) return;

            var g = e.Graphics;
            int rowsPerCol = Math.Max(1, (ClientSize.Height - 6) / ROW_H);

            float maxW = 0;
            foreach (var item in _items)
            {
                float w = g.MeasureString(item, _font).Width;
                if (w > maxW) maxW = w;
            }
            int colW = (int)maxW + COL_GAP;

            int col = 0, row = 0;
            foreach (var item in _items)
            {
                int px = col * colW + 6;
                int py = row * ROW_H + 4;

                int arrowIdx = item.IndexOf('→');
                if (arrowIdx >= 0)
                {
                    string left  = item.Substring(0, arrowIdx);
                    string arrow = "→";
                    string right = item.Substring(arrowIdx + 1);
                    float  lw    = g.MeasureString(left,  _font).Width;
                    float  aw    = g.MeasureString(arrow, _font).Width;
                    g.DrawString(left,  _font, _fgBrush,    px,           py);
                    g.DrawString(arrow, _font, _arrowBrush, px + lw,      py);
                    g.DrawString(right, _font, _fgBrush,    px + lw + aw, py);
                }
                else
                {
                    g.DrawString(item, _font, _fgBrush, px, py);
                }

                if (++row >= rowsPerCol) { row = 0; col++; }
            }
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Main form
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class ERTrackerForm : Form
    {
        // ── RAM addresses used as keys into local snapshot blocks (0x7Exxxx = SNES WRAM bus) ──
        private const long A_LOCAL_MAP = 0x7E1702;
        private const long A_PLAYER_X  = 0x7E1706;
        private const long A_PLAYER_Y  = 0x7E1707;
        private const long A_TRANS     = 0x7E06D9;
        private const long A_ON_OW     = 0x7E0CDD;
        private const long A_PLANE     = 0x7E1701;
        private const long A_NAME_SHOW = 0x7E0649;
        private const long A_NAME_TEXT = 0x7E0774;
        private const long A_IN_BATTLE = 0x7E0140;

        // Two snapshot blocks (indexed by their 0x7Exxxx SNES address):
        //   Block LO: 0x7E0140 – 0x7E0CDD  covers battle/trans/name/onOW
        //   Block HI: 0x7E1701 – 0x7E1707  covers plane/map/X/Y
        private const long BLOCK_LO_BASE = 0x7E0140;
        private const uint BLOCK_LO_SIZE = 0x0C9E;
        private const long BLOCK_HI_BASE = 0x7E1701;
        private const uint BLOCK_HI_SIZE = 7;

        // FX Pak Pro address space maps WRAM at 0xF50000.
        // To convert a 0x7Exxxx SNES address → FxPakPro: strip bank byte, add 0xF50000.
        private static uint FxPak(long snesAddr) => (uint)((snesAddr & 0xFFFF) + 0xF50000);

        private static readonly string[] PlanePrefix = { "OW", "UW", "Moon" };

        // ── SNI ──────────────────────────────────────────────────────────────
        private GrpcChannel?                       _channel;
        private Devices.DevicesClient?             _devicesClient;
        private DeviceMemory.DeviceMemoryClient?   _memClient;
        private string?                            _deviceUri;

        // Latest memory snapshot (refreshed each tick)
        private byte[] _blockLo = Array.Empty<byte>();
        private byte[] _blockHi = Array.Empty<byte>();

        // ── Timer ────────────────────────────────────────────────────────────
        private readonly System.Windows.Forms.Timer _timer;
        private bool _ticking; // prevents re-entrant async ticks

        // ── Tracker state ─────────────────────────────────────────────────────
        private bool   _init;
        private int    _mapId;
        private bool   _onOW;
        private bool   _prevTrans, _prevName;
        private int    _surfX, _surfY, _surfPlane;

        private string? _fromLabel;
        private bool    _fromOnOW;

        private Pending? _pending;

        private readonly HashSet<int>                   _transitMaps = new HashSet<int> { 0x2F };
        private readonly Dictionary<int, HashSet<int>>  _planesSeen  = new Dictionary<int, HashSet<int>>();
        private readonly Dictionary<int, string>        _nameCache   = new Dictionary<int, string>();
        private readonly HashSet<string>                _knownPairs  = new HashSet<string>(StringComparer.Ordinal);

        // ── UI ───────────────────────────────────────────────────────────────
        private ComboBox         _deviceCombo  = null!;
        private Button           _refreshBtn   = null!;
        private Button           _connectBtn   = null!;
        private ConnectionPanel  _panel        = null!;
        private Label            _statusLabel  = null!;
        private Label            _countLabel   = null!;

        // ─────────────────────────────────────────────────────────────────────
        public ERTrackerForm()
        {
            BuildUI();

            _timer          = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 fps
            _timer.Tick    += OnTimerTick;

            // Attempt auto-connect on startup
            _ = RefreshDevicesAsync(autoConnect: true);
        }

        // ── Memory read from snapshot ─────────────────────────────────────────
        private byte Rd(long addr)
        {
            if (addr >= BLOCK_LO_BASE && addr < BLOCK_LO_BASE + BLOCK_LO_SIZE)
                return _blockLo[addr - BLOCK_LO_BASE];
            if (addr >= BLOCK_HI_BASE && addr < BLOCK_HI_BASE + BLOCK_HI_SIZE)
                return _blockHi[addr - BLOCK_HI_BASE];
            throw new InvalidOperationException($"Address 0x{addr:X6} not covered by snapshot blocks");
        }

        // ── Take a memory snapshot via SNI — one MultiRead call, two segments ──
        private async Task<bool> SnapshotAsync()
        {
            if (_memClient == null || _deviceUri == null) return false;
            try
            {
                var req = new MultiReadMemoryRequest { Uri = _deviceUri };
                req.Requests.Add(new ReadMemoryRequest
                {
                    RequestAddress       = FxPak(BLOCK_LO_BASE),
                    RequestAddressSpace  = AddressSpace.FxPakPro,
                    RequestMemoryMapping = MemoryMapping.LoRom,
                    Size                 = BLOCK_LO_SIZE,
                });
                req.Requests.Add(new ReadMemoryRequest
                {
                    RequestAddress       = FxPak(BLOCK_HI_BASE),
                    RequestAddressSpace  = AddressSpace.FxPakPro,
                    RequestMemoryMapping = MemoryMapping.LoRom,
                    Size                 = BLOCK_HI_SIZE,
                });

                var resp = await _memClient.MultiReadAsync(req);
                _blockLo = resp.Responses[0].Data.ToByteArray();
                _blockHi = resp.Responses[1].Data.ToByteArray();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ── Timer tick (async event handler) ─────────────────────────────────
        private async void OnTimerTick(object? sender, EventArgs e)
        {
            if (_ticking) return;
            _ticking = true;
            try
            {
                bool ok = await SnapshotAsync();
                if (!ok)
                {
                    UpdateStatus("SNI read failed — is SNI running and device connected?");
                    return;
                }
                Tick();
            }
            catch (Exception ex)
            {
                UpdateStatus("Error: " + ex.Message);
            }
            finally
            {
                _ticking = false;
            }
        }

        // ── Per-tick logic (identical to BizHawk version) ─────────────────────
        private string PlaneStr(int p) => p < PlanePrefix.Length ? PlanePrefix[p] : "P" + p;

        private void Tick()
        {
            int  mapId  = Rd(A_LOCAL_MAP);
            int  x      = Rd(A_PLAYER_X);
            int  y      = Rd(A_PLAYER_Y);
            bool trans  = Rd(A_TRANS)     == 1;
            bool onOW   = Rd(A_ON_OW)     != 0;
            int  plane  = Rd(A_PLANE);
            bool inBat  = Rd(A_IN_BATTLE) == 1;
            bool nameOn = Rd(A_NAME_SHOW) != 0;

            bool onAnySurface = onOW;

            // First tick
            if (!_init)
            {
                _mapId = mapId; _onOW = onOW;
                _surfX = x; _surfY = y; _surfPlane = plane;
                _init = true;
                UpdateStatus("Tracking…");
            }

            // Track surface position
            if (onAnySurface && !trans)
            {
                _surfX = x; _surfY = y; _surfPlane = plane;
            }

            // Name banner appeared
            if (nameOn && !_prevName)
            {
                string? n = FF4Encoding.ReadNameBanner(Rd, A_NAME_TEXT);
                if (n != null)
                {
                    _nameCache[mapId] = n;
                    if (_pending != null && _pending.TicksWaited <= Pending.NameWindow)
                        ResolvePending(n);
                }
            }

            // Tick pending timeout
            if (_pending != null)
            {
                _pending.TicksWaited++;
                if (_pending.TicksWaited >= Pending.Timeout)
                    ResolvePending(null);
            }

            // ── Transition START ──────────────────────────────────────────────
            if (trans && !_prevTrans && !inBat)
            {
                if (_pending != null) ResolvePending(null);

                if (_onOW)
                {
                    string? name = EntranceTable.Lookup(PlaneStr(_surfPlane), _surfX, _surfY);
                    _fromLabel = name ?? $"{PlaneStr(_surfPlane)}({_surfX},{_surfY})";
                    _fromOnOW  = true;
                }
                else
                {
                    _fromLabel = null;
                    _fromOnOW  = false;
                }
            }

            // ── Transition END ────────────────────────────────────────────────
            if (!trans && _prevTrans && !inBat && _fromLabel != null)
            {
                // OW → OW (airship) — skip
                if (_fromOnOW && onOW)
                {
                    _fromLabel = null; goto Done;
                }

                // Arriving at transit map — skip
                if (_transitMaps.Contains(mapId))
                {
                    _fromLabel = null; goto Done;
                }

                _pending = new Pending
                {
                    FromLabel    = _fromLabel,
                    DestMap      = mapId,
                    DestPlane    = plane,
                    TicksWaited  = 0,
                };

                if (nameOn)
                {
                    string? n = FF4Encoding.ReadNameBanner(Rd, A_NAME_TEXT);
                    if (n != null) { _nameCache[mapId] = n; ResolvePending(n); }
                }
                else if (_nameCache.TryGetValue(mapId, out string? cached))
                {
                    ResolvePending(cached);
                }

                _fromLabel = null;
            }

            Done:
            _prevTrans = trans;
            _prevName  = nameOn;
            _mapId     = mapId;
            _onOW      = onOW;

            string locStr = onOW ? PlaneStr(plane) + " surface" : "interior";
            string state  = inBat ? "BATTLE" : (trans ? "TRANS" : locStr);
            UpdateStatus($"Map:{mapId:X2}  X:{x}  Y:{y}  [{state}]" + (_pending != null ? "  [?]" : ""));
        }

        // ── Resolve pending ───────────────────────────────────────────────────
        private void ResolvePending(string? bannerName)
        {
            if (_pending == null) return;
            var p = _pending;
            _pending = null;

            string to = bannerName
                     ?? (_nameCache.TryGetValue(p.DestMap, out string? c) ? c : null)
                     ?? $"Map_{p.DestMap:X2}";

            if (bannerName != null) _nameCache[p.DestMap] = bannerName;

            // Transit auto-detect
            if (!_planesSeen.TryGetValue(p.DestMap, out var planes))
                _planesSeen[p.DestMap] = planes = new HashSet<int>();
            planes.Add(p.DestPlane);
            if (planes.Count >= 2) { _transitMaps.Add(p.DestMap); return; }
            if (_transitMaps.Contains(p.DestMap)) return;

            // Deduplicate by name only — multiple tiles for same location count once
            string key = p.FromLabel + "|" + to;
            if (!_knownPairs.Add(key)) return;

            string line = $"{p.FromLabel}  →  {to}";
            if (_panel.InvokeRequired)
                _panel.Invoke((Action)(() => { _panel.AddItem(line); _countLabel.Text = $"{_panel.ItemCount} connection(s) found"; }));
            else
                { _panel.AddItem(line); _countLabel.Text = $"{_panel.ItemCount} connection(s) found"; }
        }

        // ── Device management ─────────────────────────────────────────────────
        private async Task RefreshDevicesAsync(bool autoConnect = false)
        {
            UpdateStatus("Contacting SNI…");
            try
            {
                // (Re)create channel each refresh so stale connections are cleaned up
                _channel?.Dispose();
                _channel        = GrpcChannel.ForAddress("http://localhost:8191");
                _devicesClient  = new Devices.DevicesClient(_channel);
                _memClient      = new DeviceMemory.DeviceMemoryClient(_channel);

                var resp = await _devicesClient.ListDevicesAsync(new DevicesRequest());

                _deviceCombo.Items.Clear();
                foreach (DevicesResponse.Types.Device d in resp.Devices)
                    _deviceCombo.Items.Add(new DeviceItem(d.Uri, d.DisplayName));

                if (_deviceCombo.Items.Count == 0)
                {
                    UpdateStatus("No devices found — is SNI running and hardware connected?");
                    return;
                }

                // Auto-select first device
                _deviceCombo.SelectedIndex = 0;

                if (autoConnect || _deviceCombo.Items.Count == 1)
                    ConnectToSelected();
                else
                    UpdateStatus($"{_deviceCombo.Items.Count} device(s) found — select one and click Connect");
            }
            catch (Exception ex)
            {
                UpdateStatus($"SNI connection failed: {ex.Message}");
            }
        }

        private void ConnectToSelected()
        {
            if (_deviceCombo.SelectedItem is not DeviceItem item) return;
            _deviceUri = item.Uri;
            _init      = false;
            _pending   = null;
            _prevTrans = false;
            _prevName  = false;
            _timer.Start();
            UpdateStatus($"Connected: {item.DisplayName}  [LoROM]");
        }

        // Small wrapper so the ComboBox displays the device name nicely
        private sealed class DeviceItem
        {
            public string Uri         { get; }
            public string DisplayName { get; }
            public DeviceItem(string uri, string name) { Uri = uri; DisplayName = name; }
            public override string ToString() => DisplayName;
        }

        // ── UI construction ───────────────────────────────────────────────────
        private void BuildUI()
        {
            Text            = "FF4FE Entrance Randomizer Tracker (SNI)";
            Size            = new Size(600, 460);
            MinimumSize     = new Size(400, 280);
            BackColor       = Color.FromArgb(18, 18, 24);
            ForeColor       = Color.FromArgb(220, 220, 235);
            Font            = new Font("Consolas", 9f);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition   = FormStartPosition.Manual;
            Location        = new Point(100, 100);

            // ── Device bar (top) ──────────────────────────────────────────────
            var deviceBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 32,
                BackColor = Color.FromArgb(22, 22, 35),
            };

            var deviceLabel = new Label
            {
                Text      = "Device:",
                AutoSize  = false,
                Width     = 52,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock      = DockStyle.Left,
                Padding   = new Padding(6, 0, 0, 0),
                ForeColor = Color.FromArgb(160, 160, 200),
            };

            _deviceCombo = new ComboBox
            {
                Dock          = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = Color.FromArgb(30, 30, 45),
                ForeColor     = Color.FromArgb(220, 220, 235),
                FlatStyle     = FlatStyle.Flat,
            };

            _refreshBtn = new Button
            {
                Text      = "⟳",
                Width     = 28,
                Dock      = DockStyle.Right,
                BackColor = Color.FromArgb(40, 40, 60),
                ForeColor = Color.FromArgb(140, 200, 255),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Consolas", 10f),
            };
            _refreshBtn.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 90);
            _refreshBtn.Click += async (s, e) => await RefreshDevicesAsync();

            _connectBtn = new Button
            {
                Text      = "Connect",
                Width     = 68,
                Dock      = DockStyle.Right,
                BackColor = Color.FromArgb(30, 80, 50),
                ForeColor = Color.FromArgb(120, 220, 150),
                FlatStyle = FlatStyle.Flat,
            };
            _connectBtn.FlatAppearance.BorderColor = Color.FromArgb(50, 120, 80);
            _connectBtn.Click += (s, e) => ConnectToSelected();

            // Add right-to-left so dock order works correctly
            deviceBar.Controls.Add(_deviceCombo);
            deviceBar.Controls.Add(_connectBtn);
            deviceBar.Controls.Add(_refreshBtn);
            deviceBar.Controls.Add(deviceLabel);

            // ── Status bar ────────────────────────────────────────────────────
            var statusBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 26,
                BackColor = Color.FromArgb(28, 28, 38),
            };
            _statusLabel = new Label
            {
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0),
                ForeColor = Color.FromArgb(120, 210, 160),
                Font      = new Font("Consolas", 8.5f),
                Text      = "Waiting for SNI…",
            };
            statusBar.Controls.Add(_statusLabel);

            // ── Connection panel (main area) ──────────────────────────────────
            _panel = new ConnectionPanel { Dock = DockStyle.Fill };

            // ── Count bar (bottom) ────────────────────────────────────────────
            var countBar = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 26,
                BackColor = Color.FromArgb(28, 28, 38),
            };
            _countLabel = new Label
            {
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0),
                ForeColor = Color.FromArgb(140, 140, 170),
                Font      = new Font("Consolas", 8.5f),
                Text      = "0 connection(s) found",
            };
            countBar.Controls.Add(_countLabel);

            // Docked controls added bottom-up
            Controls.Add(_panel);
            Controls.Add(statusBar);
            Controls.Add(deviceBar);
            Controls.Add(countBar);
        }

        private void UpdateStatus(string msg)
        {
            if (_statusLabel.InvokeRequired)
                _statusLabel.Invoke((Action)(() => _statusLabel.Text = msg));
            else
                _statusLabel.Text = msg;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _channel?.Dispose();
            base.OnFormClosed(e);
        }
    }
}