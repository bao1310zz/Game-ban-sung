using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace SpaceShooter
{
    // Class lưu thông số quái
    public class EnemyInfo { public int Hp; public int MaxHp; public int Speed; public bool IsBoss; }
    // Class lưu thông số đạn (để bắn chùm, đạn bay chéo)
    public class BulletInfo { public int Dx; public int Dy; public int Dmg; }

    public partial class Form1 : Form
    {
        Panel pnlMenu, pnlGame, pnlUpgrade;
        Timer gameTimer, countdownTimer;
        int countdownSeconds = 3;
        bool goLeft, goRight, goUp, goDown, isShooting;

        // --- CHỈ SỐ NGƯỜI CHƠI ---
        int playerSpeed = 10, bulletSpeed = 20, fireRate = 15;
        int fireCooldown = 0;
        int maxHp = 100, currentHp = 100;
        int currentXp = 0, xpToNextLevel = 50, currentLevel = 1;
        int score = 0;
        
        // Vũ khí
        int multiShot = 1; // Số nòng súng (1 đến 3)
        bool isBigBullet = false; // Đạn bự

        // --- HỆ THỐNG WAVE (ĐỢT) ---
        int currentWave = 1;
        int enemiesToSpawn = 5;
        int enemiesSpawned = 0;
        int wavePauseTimer = 0; // Nghỉ giữa các đợt

        ProgressBar pbHealth, pbXp;
        Label lblScore, lblLevel, lblCountdown, lblWave;
        PictureBox player;

        List<PictureBox> playerBullets = new List<PictureBox>();
        List<PictureBox> enemyBullets = new List<PictureBox>();
        List<PictureBox> enemies = new List<PictureBox>();
        Random rnd = new Random();

        Button[] btnUpgrades = new Button[3]; // 3 nút nâng cấp

        public Form1()
        {
            InitializeWindow();
            SetupMenuPanel();
            SetupGamePanel();
            SetupUpgradePanel();
            ShowMenu();
        }

        private void InitializeWindow()
        {
            this.Text = "Space Shooter - Survival";
            this.Size = new Size(600, 800);
            this.BackColor = Color.FromArgb(20, 20, 30);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.KeyDown += KeyIsDown;
            this.KeyUp += KeyIsUp;
        }

        // ================= GIAO DIỆN =================
        private void SetupMenuPanel()
        {
            pnlMenu = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 20, 30) };
            Label title = new Label { Text = "SPACE SURVIVAL", ForeColor = Color.Cyan, Font = new Font("Arial", 30, FontStyle.Bold), AutoSize = true, Location = new Point(80, 150) };
            Button btnNewGame = CreateButton("Chơi Mới", 200, 300, (s, e) => StartCountdown());
            pnlMenu.Controls.AddRange(new Control[] { title, btnNewGame });
            this.Controls.Add(pnlMenu);
        }

        private void SetupGamePanel()
        {
            pnlGame = new Panel { Dock = DockStyle.Fill, Visible = false };
            pbHealth = new ProgressBar { Value = 100, Maximum = 100, Width = 200, Height = 20, Location = new Point(10, 10), ForeColor = Color.Red };
            pbXp = new ProgressBar { Value = 0, Maximum = 50, Width = 200, Height = 10, Location = new Point(10, 35), ForeColor = Color.Blue };
            
            lblScore = new Label { Text = "Điểm: 0", ForeColor = Color.White, Font = new Font("Arial", 14), Location = new Point(450, 10), AutoSize = true };
            lblLevel = new Label { Text = "Lv: 1", ForeColor = Color.Yellow, Font = new Font("Arial", 14), Location = new Point(220, 10), AutoSize = true };
            lblWave = new Label { Text = "ĐỢT: 1", ForeColor = Color.Orange, Font = new Font("Arial", 20, FontStyle.Bold), Location = new Point(220, 50), AutoSize = true };
            lblCountdown = new Label { Text = "3", ForeColor = Color.White, Font = new Font("Arial", 72, FontStyle.Bold), AutoSize = true, Visible = false };
            
            player = new PictureBox { Size = new Size(60, 60), BackColor = Color.Transparent, SizeMode = PictureBoxSizeMode.StretchImage };
            LoadImageSafe(player, "Assets/player.png", Color.DodgerBlue);

            pnlGame.Controls.AddRange(new Control[] { pbHealth, pbXp, lblScore, lblLevel, lblWave, lblCountdown, player });
            this.Controls.Add(pnlGame);

            gameTimer = new Timer { Interval = 20 };
            gameTimer.Tick += GameLoop;
            countdownTimer = new Timer { Interval = 1000 };
            countdownTimer.Tick += CountdownTick;
        }

        private void SetupUpgradePanel()
        {
            pnlUpgrade = new Panel { Size = new Size(500, 400), Location = new Point(40, 150), BackColor = Color.FromArgb(50, 50, 70), Visible = false };
            Label lblUp = new Label { Text = "LÊN CẤP! CHỌN 1 NÂNG CẤP", ForeColor = Color.Gold, Font = new Font("Arial", 16, FontStyle.Bold), Location = new Point(80, 20), AutoSize = true };
            pnlUpgrade.Controls.Add(lblUp);

            for (int i = 0; i < 3; i++)
            {
                // Gán sẵn 1 sự kiện Click duy nhất cho cả 3 nút
                btnUpgrades[i] = CreateButton("Option", 50, 80 + (i * 80), UpgradeButtonClicked);
                btnUpgrades[i].Width = 400;
                pnlUpgrade.Controls.Add(btnUpgrades[i]);
            }
            this.Controls.Add(pnlUpgrade);
        }
        private void UpgradeButtonClicked(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Tag != null)
            {
                ApplyUpgrade((int)btn.Tag);
            }
        }

        private Button CreateButton(string text, int x, int y, EventHandler clickEvent)
        {
            Button btn = new Button { Text = text, Location = new Point(x, y), Size = new Size(200, 50), Font = new Font("Arial", 12, FontStyle.Bold), BackColor = Color.White };
            if (clickEvent != null) btn.Click += clickEvent;
            return btn;
        }

        // ================= VÒNG LẶP GAME & LOGIC =================
        private void GameLoop(object sender, EventArgs e)
        {
            // 1. Di chuyển người chơi (4 HƯỚNG)
            if (goLeft && player.Left > 0) player.Left -= playerSpeed;
            if (goRight && player.Left < pnlGame.Width - player.Width) player.Left += playerSpeed;
            if (goUp && player.Top > 0) player.Top -= playerSpeed;
            if (goDown && player.Bottom < pnlGame.Height) player.Top += playerSpeed;

            // 2. Bắn súng
            if (fireCooldown > 0) fireCooldown--;
            if (isShooting && fireCooldown <= 0)
            {
                ShootPlayerBullet();
                fireCooldown = fireRate;
            }

            // 3. Cập nhật Đạn Player
            for (int i = playerBullets.Count - 1; i >= 0; i--)
            {
                BulletInfo bi = (BulletInfo)playerBullets[i].Tag;
                playerBullets[i].Top += bi.Dy;
                playerBullets[i].Left += bi.Dx;
                if (playerBullets[i].Top < 0 || playerBullets[i].Left < 0 || playerBullets[i].Right > pnlGame.Width) 
                    RemoveControl(playerBullets[i], playerBullets);
            }

            // 4. Sinh & Cập nhật Quái (HỆ THỐNG WAVE)
            HandleWaveSystem();

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var en = enemies[i];
                EnemyInfo info = (EnemyInfo)en.Tag;
                en.Top += info.Speed;
                
                // Địch bắn trả (Boss bắn chùm, quái thường bắn 1)
                if (info.IsBoss && rnd.Next(0, 100) < 5) ShootEnemyBullet(en, true);
                else if (!info.IsBoss && rnd.Next(0, 100) < 2 + currentWave) ShootEnemyBullet(en, false);

                // Va chạm Người - Địch
                if (player.Bounds.IntersectsWith(en.Bounds))
                {
                    TakeDamage(info.IsBoss ? 50 : 20);
                    RemoveControl(en, enemies);
                }
                else if (en.Top > pnlGame.Height) RemoveControl(en, enemies);
            }

            // 5. Cập nhật Đạn Địch
            for (int i = enemyBullets.Count - 1; i >= 0; i--)
            {
                BulletInfo bi = (BulletInfo)enemyBullets[i].Tag;
                enemyBullets[i].Top += bi.Dy;
                enemyBullets[i].Left += bi.Dx;

                if (player.Bounds.IntersectsWith(enemyBullets[i].Bounds))
                {
                    TakeDamage(bi.Dmg);
                    RemoveControl(enemyBullets[i], enemyBullets);
                }
                else if (enemyBullets[i].Top > pnlGame.Height) RemoveControl(enemyBullets[i], enemyBullets);
            }

            CheckBulletCollisions();
            UpdateUI();
        }

        private void HandleWaveSystem()
        {
            if (wavePauseTimer > 0)
            {
                wavePauseTimer--;
                return;
            }

            // Đang trong Wave
            if (enemiesSpawned < enemiesToSpawn)
            {
                if (rnd.Next(0, 100) < 2 + currentWave) // Tỉ lệ ra quái tăng theo wave
                {
                    SpawnEnemy(false);
                    enemiesSpawned++;
                }
                // Cuối đợt ra Boss (mỗi 2 đợt ra 1 con boss)
                if (enemiesSpawned == enemiesToSpawn && currentWave % 2 == 0)
                {
                    SpawnEnemy(true);
                }
            }
            // Hết quái -> Qua Đợt mới
            else if (enemies.Count == 0)
            {
                currentWave++;
                enemiesToSpawn += 5; // Đợt sau thêm 5 quái
                enemiesSpawned = 0;
                wavePauseTimer = 100; // Nghỉ 2 giây (100 khung hình)
                lblWave.Text = $"ĐỢT: {currentWave}";
            }
        }

        private void SpawnEnemy(bool isBoss)
        {
            PictureBox en = new PictureBox { BackColor = Color.Transparent, SizeMode = PictureBoxSizeMode.StretchImage };
            EnemyInfo info = new EnemyInfo();

            if (isBoss)
            {
                en.Size = new Size(120, 120);
                en.Location = new Point(rnd.Next(0, pnlGame.Width - 120), -120);
                LoadImageSafe(en, "Assets/boss.png", Color.Purple);
                info.Hp = 100 + (currentWave * 100); // Boss máu cực trâu
                info.Speed = 2;
                info.IsBoss = true;
            }
            else
            {
                en.Size = new Size(50, 50);
                en.Location = new Point(rnd.Next(0, pnlGame.Width - 50), -50);
                LoadImageSafe(en, "Assets/enemy.png", Color.Crimson);
                info.Hp = 20 + (currentWave * 10); // Quái thường máu tăng dần
                info.Speed = rnd.Next(3, 6);
                info.IsBoss = false;
            }
            info.MaxHp = info.Hp;
            en.Tag = info;
            enemies.Add(en);
            pnlGame.Controls.Add(en);
        }

        private void ShootPlayerBullet()
        {
            int bSizeX = isBigBullet ? 15 : 8;
            int bSizeY = isBigBullet ? 30 : 20;

            // Hàm cục bộ tạo đạn
            void CreateBullet(int dx, int dy, int offsetX)
            {
                PictureBox b = new PictureBox { Size = new Size(bSizeX, bSizeY), BackColor = Color.Yellow, Location = new Point(player.Left + (player.Width/2) - (bSizeX/2) + offsetX, player.Top - bSizeY) };
                LoadImageSafe(b, "Assets/bullet.png", Color.Yellow);
                b.Tag = new BulletInfo { Dx = dx, Dy = dy, Dmg = isBigBullet ? 30 : 15 };
                playerBullets.Add(b);
                pnlGame.Controls.Add(b);
            }

            if (multiShot == 1) CreateBullet(0, -bulletSpeed, 0); // 1 nòng giữa
            else if (multiShot == 2) { CreateBullet(-2, -bulletSpeed, -15); CreateBullet(2, -bulletSpeed, 15); } // 2 nòng chéo nhẹ
            else { CreateBullet(0, -bulletSpeed, 0); CreateBullet(-5, -bulletSpeed, -20); CreateBullet(5, -bulletSpeed, 20); } // 3 nòng tủa ra
        }

        private void ShootEnemyBullet(PictureBox enemy, bool isSpread)
        {
            void CreateEnemyBullet(int dx, int dy)
            {
                PictureBox b = new PictureBox { Size = new Size(10, 10), BackColor = Color.Orange, Location = new Point(enemy.Left + enemy.Width / 2, enemy.Bottom) };
                LoadImageSafe(b, "Assets/enemy_bullet.png", Color.Orange);
                b.Tag = new BulletInfo { Dx = dx, Dy = dy, Dmg = 15 };
                enemyBullets.Add(b);
                pnlGame.Controls.Add(b);
            }

            CreateEnemyBullet(0, 10);
            if (isSpread) { CreateEnemyBullet(-5, 8); CreateEnemyBullet(5, 8); } // Boss bắn tỏa
        }

        private void CheckBulletCollisions()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                for (int j = playerBullets.Count - 1; j >= 0; j--)
                {
                    if (i < enemies.Count && j < playerBullets.Count && playerBullets[j].Bounds.IntersectsWith(enemies[i].Bounds))
                    {
                        BulletInfo bInfo = (BulletInfo)playerBullets[j].Tag;
                        EnemyInfo eInfo = (EnemyInfo)enemies[i].Tag;
                        
                        eInfo.Hp -= bInfo.Dmg; // Trừ máu quái
                        RemoveControl(playerBullets[j], playerBullets); // Xóa đạn

                        if (eInfo.Hp <= 0) // Quái chết
                        {
                            score += eInfo.IsBoss ? 100 : 10;
                            GainXp(eInfo.IsBoss ? 30 : 10);
                            RemoveControl(enemies[i], enemies);
                        }
                        break;
                    }
                }
            }
        }

        // ================= HỆ THỐNG NÂNG CẤP =================
        private void TriggerLevelUp()
        {
            gameTimer.Stop();
            goLeft = false; goRight = false; goUp = false; goDown = false; isShooting = false;
            
            // Danh sách 6 Option
            var allOptions = new List<Tuple<int, string>> {
                Tuple.Create(1, "Hồi 50 HP"),
                Tuple.Create(2, "Tăng Tốc Độ Bắn"),
                Tuple.Create(3, "Tăng Tốc Độ Đạn"),
                Tuple.Create(4, multiShot < 3 ? "Thêm Nòng Súng (+1)" : "Nâng Cấp Đạn Khổng Lồ"),
                Tuple.Create(5, "Tăng 50 Máu Tối Đa"),
                Tuple.Create(6, "Tăng Tốc Độ Di Chuyển")
            };

            // Trộn ngẫu nhiên và lấy 3 cái đầu
            var randomOptions = allOptions.OrderBy(x => rnd.Next()).Take(3).ToList();

            for (int i = 0; i < 3; i++)
            {
                btnUpgrades[i].Text = randomOptions[i].Item2;
                // Chỉ việc lưu ID của option vào cục Tag, hàm Click ở trên sẽ tự lôi ra xài
                btnUpgrades[i].Tag = randomOptions[i].Item1; 
            }

            pnlUpgrade.Visible = true;
            pnlUpgrade.BringToFront();
        }

        private void ApplyUpgrade(int type)
        {
            switch (type) {
                case 1: currentHp = Math.Min(maxHp, currentHp + 50); break;
                case 2: fireRate = Math.Max(3, fireRate - 3); break;
                case 3: bulletSpeed += 5; break;
                case 4: 
                    if (multiShot < 3) multiShot++; 
                    else isBigBullet = true; 
                    break;
                case 5: maxHp += 50; currentHp += 50; break;
                case 6: playerSpeed += 3; break;
            }

            pnlUpgrade.Visible = false;
            UpdateUI();
            
            lblCountdown.Visible = true;
            countdownSeconds = 3;
            lblCountdown.Text = "3";
            countdownTimer.Start();
            this.Focus();
        }

        // ================= TIỆN ÍCH & ĐIỀU KHIỂN =================
        private void TakeDamage(int dmg) {
            currentHp -= dmg;
            if (currentHp <= 0) {
                gameTimer.Stop();
                MessageBox.Show($"GAME OVER!\nSống sót tới đợt: {currentWave}\nĐiểm: {score}");
                ShowMenu();
            }
        }

        private void GainXp(int amount) {
            currentXp += amount;
            if (currentXp >= xpToNextLevel) {
                currentLevel++; currentXp -= xpToNextLevel; xpToNextLevel += 30;
                TriggerLevelUp();
            }
        }

        private void UpdateUI() {
            lblScore.Text = $"Điểm: {score}"; lblLevel.Text = $"Lv: {currentLevel}";
            pbHealth.Maximum = maxHp; pbHealth.Value = Math.Max(0, currentHp);
            pbXp.Maximum = xpToNextLevel; pbXp.Value = Math.Min(currentXp, xpToNextLevel);
        }

        private void ResetGameStats() {
            currentHp = 100; maxHp = 100; score = 0; currentLevel = 1; currentXp = 0; xpToNextLevel = 50; 
            fireRate = 15; bulletSpeed = 20; playerSpeed = 10; multiShot = 1; isBigBullet = false;
            currentWave = 1; enemiesSpawned = 0; enemiesToSpawn = 5;
            player.Location = new Point(270, 600);
            ClearAllEntities(); UpdateUI();
        }

        private void ClearAllEntities() {
            foreach (var b in playerBullets) pnlGame.Controls.Remove(b);
            foreach (var b in enemyBullets) pnlGame.Controls.Remove(b);
            foreach (var e in enemies) pnlGame.Controls.Remove(e);
            playerBullets.Clear(); enemyBullets.Clear(); enemies.Clear();
        }

        private void RemoveControl(PictureBox pb, List<PictureBox> list) { pnlGame.Controls.Remove(pb); list.Remove(pb); pb.Dispose(); }
        private void LoadImageSafe(PictureBox pb, string path, Color fallback) { try { pb.Image = Image.FromFile(path); pb.BackColor = Color.Transparent; } catch { pb.BackColor = fallback; } }
        private void ShowMenu() { pnlMenu.Visible = true; pnlGame.Visible = false; pnlUpgrade.Visible = false; gameTimer.Stop(); }
        private void StartCountdown() {
            ResetGameStats(); pnlMenu.Visible = false; pnlGame.Visible = true; lblCountdown.Visible = true;
            lblCountdown.Location = new Point(this.ClientSize.Width / 2 - 40, this.ClientSize.Height / 2 - 50);
            countdownSeconds = 3; lblCountdown.Text = "3"; countdownTimer.Start(); this.Focus();
        }
        private void CountdownTick(object sender, EventArgs e) {
            countdownSeconds--;
            if (countdownSeconds > 0) lblCountdown.Text = countdownSeconds.ToString();
            else { lblCountdown.Visible = false; countdownTimer.Stop(); gameTimer.Start(); }
        }

        private void KeyIsDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.A) goLeft = true;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.D) goRight = true;
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.W) goUp = true;
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.S) goDown = true;
            if (e.KeyCode == Keys.Space) isShooting = true;
        }
        private void KeyIsUp(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.A) goLeft = false;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.D) goRight = false;
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.W) goUp = false;
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.S) goDown = false;
            if (e.KeyCode == Keys.Space) isShooting = false;
        }
    }
}