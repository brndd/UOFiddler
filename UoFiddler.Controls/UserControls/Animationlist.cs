/***************************************************************************
 *
 * $Author: Turley
 * 
 * "THE BEER-WARE LICENSE"
 * As long as you retain this notice you can do whatever you want with 
 * this stuff. If we meet some day, and you think this stuff is worth it,
 * you can buy me a beer in return.
 *
 ***************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using Ultima;
using UoFiddler.Controls.Classes;
using UoFiddler.Controls.Forms;
using AnimationEdit = UoFiddler.Controls.Forms.AnimationEdit;

namespace UoFiddler.Controls.UserControls
{
    public partial class AnimationList : UserControl
    {
        public AnimationList()
        {
            InitializeComponent();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        public string[][] GetAnimNames { get; } = {
            // Monster
            new[]
            {
                "Walk", "Idle", "Die1", "Die2", "Attack1", "Attack2", "Attack3", "AttackBow", "AttackCrossBow",
                "AttackThrow", "GetHit", "Pillage", "Stomp", "Cast2", "Cast3", "BlockRight", "BlockLeft", "Idle",
                "Fidget", "Fly", "TakeOff", "GetHitInAir"
            },
            // Sea
            new[] { "Walk", "Run", "Idle", "Idle", "Fidget", "Attack1", "Attack2", "GetHit", "Die1" },
            // Animal
            new[]
            {
                "Walk", "Run", "Idle", "Eat", "Alert", "Attack1", "Attack2", "GetHit", "Die1", "Idle", "Fidget",
                "LieDown", "Die2"
            },
            // Human
            new[]
            {
                "Walk_01", "WalkStaff_01", "Run_01", "RunStaff_01", "Idle_01", "Idle_01", "Fidget_Yawn_Stretch_01",
                "CombatIdle1H_01", "CombatIdle1H_01", "AttackSlash1H_01", "AttackPierce1H_01", "AttackBash1H_01",
                "AttackBash2H_01", "AttackSlash2H_01", "AttackPierce2H_01", "CombatAdvance_1H_01", "Spell1",
                "Spell2", "AttackBow_01", "AttackCrossbow_01", "GetHit_Fr_Hi_01", "Die_Hard_Fwd_01",
                "Die_Hard_Back_01", "Horse_Walk_01", "Horse_Run_01", "Horse_Idle_01",
                "Horse_Attack1H_SlashRight_01", "Horse_AttackBow_01", "Horse_AttackCrossbow_01",
                "Horse_Attack2H_SlashRight_01", "Block_Shield_Hard_01", "Punch_Punch_Jab_01", "Bow_Lesser_01",
                "Salute_Armed1h_01", "Ingest_Eat_01"
            }
        };

        private Bitmap _mainPicture;
        private int _currentSelect;
        private int _currentSelectAction;
        private bool _animate;
        private int _frameIndex;
        private Bitmap[] _animation;
        private bool _imageInvalidated = true;
        private Timer _timer;
        private Frame[] _frames;
        private int _customHue;
        private int _defHue;
        private int _facing = 1;
        private bool _sortAlpha;
        private int _displayType;
        private bool _loaded;

        /// <summary>
        /// ReLoads if loaded
        /// </summary>
        private void Reload()
        {
            if (!_loaded)
            {
                return;
            }

            _mainPicture = null;
            _currentSelect = 0;
            _currentSelectAction = 0;
            _animate = false;
            _imageInvalidated = true;
            StopAnimation();
            _frames = null;
            _customHue = 0;
            _defHue = 0;
            _facing = 1;
            _sortAlpha = false;
            _displayType = 0;
            OnLoad(this, EventArgs.Empty);
        }

        private void OnLoad(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            Options.LoadedUltimaClass["Animations"] = true;
            Options.LoadedUltimaClass["Hues"] = true;
            TreeViewMobs.TreeViewNodeSorter = new GraphicSorter();
            if (!LoadXml())
            {
                Cursor.Current = Cursors.Default;
                return;
            }

            LoadListView();

            extractAnimationToolStripMenuItem.Visible = false;
            _currentSelect = 0;
            _currentSelectAction = 0;
            if (TreeViewMobs.Nodes[0].Nodes.Count > 0)
            {
                TreeViewMobs.SelectedNode = TreeViewMobs.Nodes[0].Nodes[0];
            }

            FacingBar.Value = (_facing + 3) & 7;
            if (!_loaded)
            {
                ControlEvents.FilePathChangeEvent += OnFilePathChangeEvent;
            }

            _loaded = true;
            Cursor.Current = Cursors.Default;
        }

        private void OnFilePathChangeEvent()
        {
            Reload();
        }

        /// <summary>
        /// Changes Hue of current Mob
        /// </summary>
        /// <param name="select"></param>
        public void ChangeHue(int select)
        {
            _customHue = select + 1;
            CurrentSelect = CurrentSelect;
        }

        /// <summary>
        /// Is Graphic already in TreeView
        /// </summary>
        /// <param name="graphic"></param>
        /// <returns></returns>
        public bool IsAlreadyDefined(int graphic)
        {
            foreach (TreeNode node in TreeViewMobs.Nodes[0].Nodes)
            {
                if (((int[])node.Tag)[0] == graphic)
                {
                    return true;
                }
            }

            foreach (TreeNode node in TreeViewMobs.Nodes[1].Nodes)
            {
                if (((int[])node.Tag)[0] == graphic)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds Graphic with type and name to List
        /// </summary>
        /// <param name="graphic"></param>
        /// <param name="type"></param>
        /// <param name="name"></param>
        public void AddGraphic(int graphic, int type, string name)
        {
            TreeViewMobs.BeginUpdate();
            TreeViewMobs.TreeViewNodeSorter = null;
            TreeNode nodeParent = new TreeNode(name)
            {
                Tag = new[] { graphic, type },
                ToolTipText = Animations.GetFileName(graphic)
            };

            if (type == 4)
            {
                TreeViewMobs.Nodes[1].Nodes.Add(nodeParent);
                type = 3;
            }
            else
            {
                TreeViewMobs.Nodes[0].Nodes.Add(nodeParent);
            }

            for (int i = 0; i < GetAnimNames[type].GetLength(0); ++i)
            {
                if (!Animations.IsActionDefined(graphic, i, 0))
                {
                    continue;
                }

                TreeNode node = new TreeNode($"{i} {GetAnimNames[type][i]}")
                {
                    Tag = i
                };

                nodeParent.Nodes.Add(node);
            }
            TreeViewMobs.TreeViewNodeSorter = !_sortAlpha
                ? new GraphicSorter()
                : (IComparer)new AlphaSorter();

            TreeViewMobs.Sort();
            TreeViewMobs.EndUpdate();
            LoadListView();
            TreeViewMobs.SelectedNode = nodeParent;
            nodeParent.EnsureVisible();
        }

        private bool Animate
        {
            get => _animate;
            set
            {
                if (_animate == value)
                {
                    return;
                }

                _animate = value;
                extractAnimationToolStripMenuItem.Visible = _animate;
                StopAnimation();
                _imageInvalidated = true;
                MainPictureBox.Invalidate();
            }
        }

        private void StopAnimation()
        {
            if (_timer != null)
            {
                if (_timer.Enabled)
                {
                    _timer.Stop();
                }

                _timer.Dispose();
                _timer = null;
            }

            if (_animation != null)
            {
                for (int i = 0; i < _animation.Length; ++i)
                {
                    _animation[i]?.Dispose();
                }
            }

            _animation = null;
            _frameIndex = 0;
        }

        private int CurrentSelect
        {
            get => _currentSelect;
            set
            {
                _currentSelect = value;
                if (_timer != null)
                {
                    if (_timer.Enabled)
                    {
                        _timer.Stop();
                    }

                    _timer.Dispose();
                    _timer = null;
                }
                SetPicture();
                MainPictureBox.Invalidate();
            }
        }

        private void SetPicture()
        {
            _frames = null;
            _mainPicture?.Dispose();
            if (_currentSelect == 0)
            {
                return;
            }

            if (Animate)
            {
                _mainPicture = DoAnimation();
            }
            else
            {
                int body = _currentSelect;
                Animations.Translate(ref body);
                int hue = _customHue;
                if (hue != 0)
                {
                    _frames = Animations.GetAnimation(_currentSelect, _currentSelectAction, _facing, ref hue, true, false);
                }
                else
                {
                    _frames = Animations.GetAnimation(_currentSelect, _currentSelectAction, _facing, ref hue, false, false);
                    _defHue = hue;
                }

                if (_frames != null)
                {
                    if (_frames[0].Bitmap != null)
                    {
                        _mainPicture = new Bitmap(_frames[0].Bitmap);
                        BaseGraphicLabel.Text = $"BaseGraphic: {body}";
                        GraphicLabel.Text = $"Graphic: {_currentSelect}(0x{_currentSelect:X})";
                        HueLabel.Text = $"Hue: {hue + 1}";
                    }
                    else
                    {
                        _mainPicture = null;
                    }
                }
                else
                {
                    _mainPicture = null;
                }
            }
        }

        private Bitmap DoAnimation()
        {
            if (_timer != null)
            {
                return _animation[_frameIndex] != null
                    ? new Bitmap(_animation[_frameIndex])
                    : null;
            }

            int body = _currentSelect;
            Animations.Translate(ref body);
            int hue = _customHue;
            if (hue != 0)
            {
                _frames = Animations.GetAnimation(_currentSelect, _currentSelectAction, _facing, ref hue, true, false);
            }
            else
            {
                _frames = Animations.GetAnimation(_currentSelect, _currentSelectAction, _facing, ref hue, false, false);
                _defHue = hue;
            }

            if (_frames == null)
            {
                return null;
            }

            BaseGraphicLabel.Text = $"BaseGraphic: {body}";
            GraphicLabel.Text = $"Graphic: {_currentSelect}(0x{_currentSelect:X})";
            HueLabel.Text = $"Hue: {hue + 1}";
            int count = _frames.Length;
            _animation = new Bitmap[count];

            for (int i = 0; i < count; ++i)
            {
                _animation[i] = _frames[i].Bitmap;
            }

            _timer = new Timer
            {
                Interval = 1000 / count
            };
            _timer.Tick += AnimTick;
            _timer.Start();

            _frameIndex = 0;
            LoadListViewFrames(); // Reload FrameTab

            return _animation[0] != null ? new Bitmap(_animation[0]) : null;
        }

        private void AnimTick(object sender, EventArgs e)
        {
            ++_frameIndex;

            if (_frameIndex == _animation.Length)
            {
                _frameIndex = 0;
            }

            _imageInvalidated = true;
            MainPictureBox.Invalidate();
        }

        private void OnPaint_MainPicture(object sender, PaintEventArgs e)
        {
            if (_imageInvalidated)
            {
                SetPicture();
            }

            if (_mainPicture != null)
            {
                Point location = Point.Empty;
                Size size = _mainPicture.Size;
                location.X = (MainPictureBox.Width - _mainPicture.Width) / 2;
                location.Y = (MainPictureBox.Height - _mainPicture.Height) / 2;

                Rectangle destRect = new Rectangle(location, size);

                e.Graphics.DrawImage(_mainPicture, destRect, 0, 0, _mainPicture.Width, _mainPicture.Height, GraphicsUnit.Pixel);
            }
            else
            {
                _mainPicture = null;
            }
        }

        private void TreeViewMobs_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Parent != null)
            {
                if (e.Node.Parent.Name == "Mobs" || e.Node.Parent.Name == "Equipment")
                {
                    _currentSelectAction = 0;
                    CurrentSelect = ((int[])e.Node.Tag)[0];
                    if (e.Node.Parent.Name == "Mobs" && _displayType == 1)
                    {
                        _displayType = 0;
                        LoadListView();
                    }
                    else if (e.Node.Parent.Name == "Equipment" && _displayType == 0)
                    {
                        _displayType = 1;
                        LoadListView();
                    }
                }
                else
                {
                    _currentSelectAction = (int)e.Node.Tag;
                    CurrentSelect = ((int[])e.Node.Parent.Tag)[0];
                    if (e.Node.Parent.Parent.Name == "Mobs" && _displayType == 1)
                    {
                        _displayType = 0;
                        LoadListView();
                    }
                    else if (e.Node.Parent.Parent.Name == "Equipment" && _displayType == 0)
                    {
                        _displayType = 1;
                        LoadListView();
                    }
                }
            }
            else
            {
                if (e.Node.Name == "Mobs" && _displayType == 1)
                {
                    _displayType = 0;
                    LoadListView();
                }
                else if (e.Node.Name == "Equipment" && _displayType == 0)
                {
                    _displayType = 1;
                    LoadListView();
                }
                TreeViewMobs.SelectedNode = e.Node.Nodes[0];
            }
        }

        private void Animate_Click(object sender, EventArgs e)
        {
            Animate = !Animate;
        }

        private bool LoadXml()
        {
            string path = Options.AppDataPath;

            string fileName = Path.Combine(path, "Animationlist.xml");
            if (!File.Exists(fileName))
            {
                return false;
            }

            TreeViewMobs.BeginUpdate();
            TreeViewMobs.Nodes.Clear();

            XmlDocument dom = new XmlDocument();
            dom.Load(fileName);
            XmlElement xMobs = dom["Graphics"];
            List<TreeNode> nodes = new List<TreeNode>();
            TreeNode node;
            TreeNode typeNode;
            TreeNode rootNode = new TreeNode("Mobs")
            {
                Name = "Mobs",
                Tag = -1
            };
            nodes.Add(rootNode);

            foreach (XmlElement xMob in xMobs.SelectNodes("Mob"))
            {
                string name = xMob.GetAttribute("name");
                int value = int.Parse(xMob.GetAttribute("body"));
                int type = int.Parse(xMob.GetAttribute("type"));
                node = new TreeNode(name)
                {
                    Tag = new[] { value, type },
                    ToolTipText = Animations.GetFileName(value)
                };
                rootNode.Nodes.Add(node);

                for (int i = 0; i < GetAnimNames[type].GetLength(0); ++i)
                {
                    if (!Animations.IsActionDefined(value, i, 0))
                    {
                        continue;
                    }

                    typeNode = new TreeNode($"{i} {GetAnimNames[type][i]}")
                    {
                        Tag = i
                    };
                    node.Nodes.Add(typeNode);
                }
            }
            rootNode = new TreeNode("Equipment")
            {
                Name = "Equipment",
                Tag = -2
            };
            nodes.Add(rootNode);

            foreach (XmlElement xMob in xMobs.SelectNodes("Equip"))
            {
                string name = xMob.GetAttribute("name");
                int value = int.Parse(xMob.GetAttribute("body"));
                int type = int.Parse(xMob.GetAttribute("type"));
                node = new TreeNode(name)
                {
                    Tag = new[] { value, type },
                    ToolTipText = Animations.GetFileName(value)
                };
                rootNode.Nodes.Add(node);

                for (int i = 0; i < GetAnimNames[type].GetLength(0); ++i)
                {
                    if (!Animations.IsActionDefined(value, i, 0))
                    {
                        continue;
                    }

                    typeNode = new TreeNode($"{i} {GetAnimNames[type][i]}")
                    {
                        Tag = i
                    };
                    node.Nodes.Add(typeNode);
                }
            }
            TreeViewMobs.Nodes.AddRange(nodes.ToArray());
            nodes.Clear();
            TreeViewMobs.EndUpdate();
            return true;
        }

        private void LoadListView()
        {
            listView.BeginUpdate();
            listView.Clear();
            foreach (TreeNode node in TreeViewMobs.Nodes[_displayType].Nodes)
            {
                ListViewItem item = new ListViewItem($"({((int[])node.Tag)[0]})", 0)
                {
                    Tag = ((int[])node.Tag)[0]
                };
                listView.Items.Add(item);
            }
            listView.EndUpdate();
        }

        private void SelectChanged_listView(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count > 0)
            {
                TreeViewMobs.SelectedNode = TreeViewMobs.Nodes[_displayType].Nodes[listView.SelectedItems[0].Index];
            }
        }

        private void ListView_DoubleClick(object sender, MouseEventArgs e)
        {
            tabControl1.SelectTab(tabPage1);
        }

        private void ListViewDrawItem(object sender, DrawListViewItemEventArgs e)
        {
            int graphic = (int)e.Item.Tag;
            int hue = 0;
            _frames = Animations.GetAnimation(graphic, 0, 1, ref hue, false, true);

            if (_frames == null)
            {
                return;
            }

            Bitmap bmp = _frames[0].Bitmap;
            int width = bmp.Width;
            int height = bmp.Height;

            if (width > e.Bounds.Width)
            {
                width = e.Bounds.Width;
            }

            if (height > e.Bounds.Height)
            {
                height = e.Bounds.Height;
            }

            e.Graphics.DrawImage(bmp, e.Bounds.X, e.Bounds.Y, width, height);
            e.DrawText(TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter);
            if (listView.SelectedItems.Contains(e.Item))
            {
                e.DrawFocusRectangle();
            }
            else
            {
                e.Graphics.DrawRectangle(new Pen(Color.Gray), e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height);
            }
        }

        private HuePopUp _showForm;

        private void OnClick_Hue(object sender, EventArgs e)
        {
            if (_showForm?.IsDisposed == false)
            {
                return;
            }

            _showForm = _customHue == 0 ? new HuePopUp(this, _defHue + 1) : new HuePopUp(this, _customHue - 1);

            _showForm.TopMost = true;
            _showForm.Show();
        }

        private void LoadListViewFrames()
        {
            listView1.BeginUpdate();
            listView1.Clear();
            for (int frame = 0; frame < _animation.Length; ++frame)
            {
                ListViewItem item = new ListViewItem(frame.ToString(), 0)
                {
                    Tag = frame
                };
                listView1.Items.Add(item);
            }
            listView1.EndUpdate();
        }

        private void Frames_ListView_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            Bitmap bmp = _animation[(int)e.Item.Tag];
            int width = bmp.Width;
            int height = bmp.Height;

            if (width > e.Bounds.Width)
            {
                width = e.Bounds.Width;
            }

            if (height > e.Bounds.Height)
            {
                height = e.Bounds.Height;
            }

            e.Graphics.DrawImage(bmp, e.Bounds.X, e.Bounds.Y, width, height);
            e.DrawText(TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter);
            e.Graphics.DrawRectangle(new Pen(Color.Gray), e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height);
        }

        private void OnScrollFacing(object sender, EventArgs e)
        {
            _facing = (FacingBar.Value - 3) & 7;
            CurrentSelect = CurrentSelect;
        }

        private void OnClick_Sort(object sender, EventArgs e)
        {
            _sortAlpha = !_sortAlpha;
            TreeViewMobs.BeginUpdate();
            TreeViewMobs.TreeViewNodeSorter = !_sortAlpha
                ? new GraphicSorter()
                : (IComparer)new AlphaSorter();

            TreeViewMobs.Sort();
            TreeViewMobs.EndUpdate();
            LoadListView();
        }

        private void OnClickRemove(object sender, EventArgs e)
        {
            TreeNode node = TreeViewMobs.SelectedNode;
            if (node?.Parent == null)
            {
                return;
            }

            if (node.Parent.Name != "Mobs" && node.Parent.Name != "Equipment")
            {
                node = node.Parent;
            }

            node.Remove();
            LoadListView();
        }

        private AnimationEdit _animEditEntry;

        private void OnClickAnimationEdit(object sender, EventArgs e)
        {
            if (_animEditEntry?.IsDisposed == false)
            {
                return;
            }

            _animEditEntry = new AnimationEdit();
            //animEditEntry.TopMost = true;
            _animEditEntry.Show();
        }

        private AnimationListNewEntries _animNewEntry;

        private void OnClickFindNewEntries(object sender, EventArgs e)
        {
            if (_animNewEntry?.IsDisposed == false)
            {
                return;
            }

            _animNewEntry = new AnimationListNewEntries(this)
            {
                TopMost = true
            };
            _animNewEntry.Show();
        }

        private void RewriteXml(object sender, EventArgs e)
        {
            TreeViewMobs.BeginUpdate();
            TreeViewMobs.TreeViewNodeSorter = new GraphicSorter();
            TreeViewMobs.Sort();
            TreeViewMobs.EndUpdate();

            string filepath = Options.AppDataPath;

            string fileName = Path.Combine(filepath, "Animationlist.xml");

            XmlDocument dom = new XmlDocument();
            XmlDeclaration decl = dom.CreateXmlDeclaration("1.0", "utf-8", null);
            dom.AppendChild(decl);
            XmlElement sr = dom.CreateElement("Graphics");
            XmlComment comment = dom.CreateComment("Entries in Mob tab");
            sr.AppendChild(comment);
            comment = dom.CreateComment("Name=Displayed name");
            sr.AppendChild(comment);
            comment = dom.CreateComment("body=Graphic");
            sr.AppendChild(comment);
            comment = dom.CreateComment("type=0:Monster, 1:Sea, 2:Animal, 3:Human/Equipment");
            sr.AppendChild(comment);

            XmlElement elem;
            foreach (TreeNode node in TreeViewMobs.Nodes[0].Nodes)
            {
                elem = dom.CreateElement("Mob");
                elem.SetAttribute("name", node.Text);
                elem.SetAttribute("body", ((int[])node.Tag)[0].ToString());
                elem.SetAttribute("type", ((int[])node.Tag)[1].ToString());

                sr.AppendChild(elem);
            }
            foreach (TreeNode node in TreeViewMobs.Nodes[1].Nodes)
            {
                elem = dom.CreateElement("Equip");
                elem.SetAttribute("name", node.Text);
                elem.SetAttribute("body", ((int[])node.Tag)[0].ToString());
                elem.SetAttribute("type", ((int[])node.Tag)[1].ToString());
                sr.AppendChild(elem);
            }
            dom.AppendChild(sr);
            dom.Save(fileName);
            MessageBox.Show("XML saved", "Rewrite", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
        }

        private void Extract_Image_ClickBmp(object sender, EventArgs e)
        {
            string path = Options.OutputPath;
            string what = "Mob";
            if (_displayType == 1)
            {
                what = "Equipment";
            }

            string fileName = Path.Combine(path, $"{what} {_currentSelect}.bmp");

            if (Animate)
            {
                using (Bitmap newBitmap = new Bitmap(_animation[0].Width, _animation[0].Height))
                {
                    Graphics newGraph = Graphics.FromImage(newBitmap);
                    newGraph.FillRectangle(Brushes.White, 0, 0, newBitmap.Width, newBitmap.Height);
                    newGraph.DrawImage(_animation[0], new Point(0, 0));
                    newGraph.Save();
                    newBitmap.Save(fileName, ImageFormat.Bmp);
                }
            }
            else
            {
                using (Bitmap newBitmap = new Bitmap(_mainPicture.Width, _mainPicture.Height))
                {
                    Graphics newGraph = Graphics.FromImage(newBitmap);
                    newGraph.FillRectangle(Brushes.White, 0, 0, newBitmap.Width, newBitmap.Height);
                    newGraph.DrawImage(_mainPicture, new Point(0, 0));
                    newGraph.Save();
                    newBitmap.Save(fileName, ImageFormat.Bmp);
                }
            }
            MessageBox.Show(
                $"{what} saved to {fileName}",
                "Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
        }

        private void Extract_Image_ClickTiff(object sender, EventArgs e)
        {
            string path = Options.OutputPath;
            string what = "Mob";
            if (_displayType == 1)
            {
                what = "Equipment";
            }

            string fileName = Path.Combine(path, $"{what} {_currentSelect}.tiff");

            if (Animate)
            {
                using (Bitmap newBitmap = new Bitmap(_animation[0].Width, _animation[0].Height))
                {
                    Graphics newGraph = Graphics.FromImage(newBitmap);
                    newGraph.FillRectangle(Brushes.White, 0, 0, newBitmap.Width, newBitmap.Height);
                    newGraph.DrawImage(_animation[0], new Point(0, 0));
                    newGraph.Save();
                    newBitmap.Save(fileName, ImageFormat.Tiff);
                }
            }
            else
            {
                using (Bitmap newBitmap = new Bitmap(_mainPicture.Width, _mainPicture.Height))
                {
                    Graphics newGraph = Graphics.FromImage(newBitmap);
                    newGraph.FillRectangle(Brushes.White, 0, 0, newBitmap.Width, newBitmap.Height);
                    newGraph.DrawImage(_mainPicture, new Point(0, 0));
                    newGraph.Save();
                    newBitmap.Save(fileName, ImageFormat.Tiff);
                }
            }
            MessageBox.Show(
                $"{what} saved to {fileName}",
                "Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
        }

        private void Extract_Image_ClickJpg(object sender, EventArgs e)
        {
            string path = Options.OutputPath;
            string what = "Mob";
            if (_displayType == 1)
            {
                what = "Equipment";
            }

            string fileName = Path.Combine(path, $"{what} {_currentSelect}.jpg");

            if (Animate)
            {
                using (Bitmap newBitmap = new Bitmap(_animation[0].Width, _animation[0].Height))
                {
                    Graphics newGraph = Graphics.FromImage(newBitmap);
                    newGraph.FillRectangle(Brushes.White, 0, 0, newBitmap.Width, newBitmap.Height);
                    newGraph.DrawImage(_animation[0], new Point(0, 0));
                    newGraph.Save();
                    newBitmap.Save(fileName, ImageFormat.Jpeg);
                }
            }
            else
            {
                using (Bitmap newBitmap = new Bitmap(_mainPicture.Width, _mainPicture.Height))
                {
                    Graphics newGraph = Graphics.FromImage(newBitmap);
                    newGraph.FillRectangle(Brushes.White, 0, 0, newBitmap.Width, newBitmap.Height);
                    newGraph.DrawImage(_mainPicture, new Point(0, 0));
                    newGraph.Save();
                    newBitmap.Save(fileName, ImageFormat.Tiff);
                }
            }
            MessageBox.Show(
                $"{what} saved to {fileName}",
                "Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
        }

        private void OnClickExtractAnimBmp(object sender, EventArgs e)
        {
            string path = Options.OutputPath;
            string what = "Mob";
            if (_displayType == 1)
            {
                what = "Equipment";
            }

            string fileName = Path.Combine(path, $"{what} {_currentSelect}");
            if (!Animate)
            {
                return;
            }

            for (int i = 0; i < _animation.Length; ++i)
            {
                Bitmap newBitmap = new Bitmap(_animation[i].Width, _animation[i].Height);
                Graphics newGraph = Graphics.FromImage(newBitmap);
                newGraph.FillRectangle(Brushes.White, 0, 0, newBitmap.Width, newBitmap.Height);
                newGraph.DrawImage(_animation[i], new Point(0, 0));
                newGraph.Save();
                newBitmap.Save($"{fileName}-{i}.bmp", ImageFormat.Bmp);
            }
            MessageBox.Show(
                $"{what} saved to '{fileName}-X.bmp'",
                "Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
        }

        private void OnClickExtractAnimTiff(object sender, EventArgs e)
        {
            string path = Options.OutputPath;
            string what = "Mob";
            if (_displayType == 1)
            {
                what = "Equipment";
            }

            string fileName = Path.Combine(path, $"{what} {_currentSelect}");
            if (!Animate)
            {
                return;
            }

            for (int i = 0; i < _animation.Length; ++i)
            {
                using (Bitmap newBitmap = new Bitmap(_animation[i].Width, _animation[i].Height))
                {
                    Graphics newGraph = Graphics.FromImage(newBitmap);
                    newGraph.FillRectangle(Brushes.White, 0, 0, newBitmap.Width, newBitmap.Height);
                    newGraph.DrawImage(_animation[i], new Point(0, 0));
                    newGraph.Save();
                    newBitmap.Save($"{fileName}-{i}.tiff", ImageFormat.Tiff);
                }
            }
            MessageBox.Show(
                $"{what} saved to '{fileName}-X.tiff'",
                "Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
        }

        private void OnClickExtractAnimJpg(object sender, EventArgs e)
        {
            string path = Options.OutputPath;
            string what = "Mob";
            if (_displayType == 1)
            {
                what = "Equipment";
            }

            string fileName = Path.Combine(path, $"{what} {_currentSelect}");
            if (!Animate)
            {
                return;
            }

            for (int i = 0; i < _animation.Length; ++i)
            {
                using (Bitmap newBitmap = new Bitmap(_animation[i].Width, _animation[i].Height))
                {
                    Graphics newGraph = Graphics.FromImage(newBitmap);
                    newGraph.FillRectangle(Brushes.White, 0, 0, newBitmap.Width, newBitmap.Height);
                    newGraph.DrawImage(_animation[i], new Point(0, 0));
                    newGraph.Save();
                    newBitmap.Save($"{fileName}-{i}.jpg", ImageFormat.Jpeg);
                }
            }
            MessageBox.Show(
                $"{what} saved to '{fileName}-X.jpg'",
                "Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
        }

        private void OnClickExportFrameBmp(object sender, EventArgs e)
        {
            string path = Options.OutputPath;
            string what = "Mob";
            if (_displayType == 1)
            {
                what = "Equipment";
            }

            if (listView1.SelectedItems.Count < 1)
            {
                return;
            }

            string fileName = Path.Combine(path, $"{what} {_currentSelect}");

            Bitmap bit = _animation[(int)listView1.SelectedItems[0].Tag];
            using (Bitmap newBitmap = new Bitmap(bit.Width, bit.Height))
            {
                Graphics newGraph = Graphics.FromImage(newBitmap);
                newGraph.FillRectangle(Brushes.White, 0, 0, newBitmap.Width, newBitmap.Height);
                newGraph.DrawImage(bit, new Point(0, 0));
                newGraph.Save();
                newBitmap.Save($"{fileName}-{(int)listView1.SelectedItems[0].Tag}.bmp", ImageFormat.Bmp);
            }
        }

        private void OnClickExportFrameTiff(object sender, EventArgs e)
        {
            string path = Options.OutputPath;
            string what = "Mob";
            if (_displayType == 1)
            {
                what = "Equipment";
            }

            if (listView1.SelectedItems.Count < 1)
            {
                return;
            }

            string fileName = Path.Combine(path, $"{what} {_currentSelect}");

            Bitmap bit = _animation[(int)listView1.SelectedItems[0].Tag];
            using (Bitmap newBmp = new Bitmap(bit.Width, bit.Height))
            {
                Graphics newGraph = Graphics.FromImage(newBmp);
                newGraph.FillRectangle(Brushes.White, 0, 0, newBmp.Width, newBmp.Height);
                newGraph.DrawImage(bit, new Point(0, 0));
                newGraph.Save();
                newBmp.Save($"{fileName}-{(int)listView1.SelectedItems[0].Tag}.tiff", ImageFormat.Tiff);
            }
        }

        private void OnClickExportFrameJpg(object sender, EventArgs e)
        {
            string path = Options.OutputPath;
            string what = "Mob";
            if (_displayType == 1)
            {
                what = "Equipment";
            }

            if (listView1.SelectedItems.Count < 1)
            {
                return;
            }

            string fileName = Path.Combine(path, $"{what} {_currentSelect}");

            Bitmap bit = _animation[(int)listView1.SelectedItems[0].Tag];
            using (Bitmap newBitmap = new Bitmap(bit.Width, bit.Height))
            {
                Graphics newGraph = Graphics.FromImage(newBitmap);
                newGraph.FillRectangle(Brushes.White, 0, 0, newBitmap.Width, newBitmap.Height);
                newGraph.DrawImage(bit, new Point(0, 0));
                newGraph.Save();

                newBitmap.Save($"{fileName}-{(int)listView1.SelectedItems[0].Tag}.jpg", ImageFormat.Jpeg);
            }
        }
    }

    public class AlphaSorter : IComparer
    {
        public int Compare(object x, object y)
        {
            TreeNode tx = x as TreeNode;
            TreeNode ty = y as TreeNode;
            if (tx.Parent == null)  // dont change Mob and Equipment
            {
                return (int)tx.Tag == -1 ? -1 : 1;
            }
            if (tx.Parent.Parent != null)
            {
                return (int)tx.Tag - (int)ty.Tag;
            }

            return string.CompareOrdinal(tx.Text, ty.Text);
        }
    }

    public class GraphicSorter : IComparer
    {
        public int Compare(object x, object y)
        {
            TreeNode tx = x as TreeNode;
            TreeNode ty = y as TreeNode;
            if (tx.Parent == null)
            {
                return (int)tx.Tag == -1 ? -1 : 1;
            }

            if (tx.Parent.Parent != null)
            {
                return (int)tx.Tag - (int)ty.Tag;
            }

            int[] ix = (int[])tx.Tag;
            int[] iy = (int[])ty.Tag;

            if (ix[0] == iy[0])
            {
                return 0;
            }

            if (ix[0] < iy[0])
            {
                return -1;
            }

            return 1;
        }
    }
}
