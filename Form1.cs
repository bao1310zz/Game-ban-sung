using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Timer = System.Windows.Forms.Timer;

namespace SpaceShooter
{
    public partial class Form1 : Form
    {
        // --- CÁC BIẾN TRẠNG THÁI GAME ---
        Panel pnlMenu, pnlGame, pnlUpgrade;
        Timer gameTimer, countdownTimer;
        int countdownSeconds = 3;
        bool goLeft, goRight, isShooting;

        // Chỉ số người chơi
        int playerSpeed = 10, bulletSpeed = 20, fireRate = 15;
        int fireCooldown = 0;
        int maxHp = 100, currentHp = 100;
        int currentXp = 0, xpToNextLevel = 50, currentLevel = 1;
        int score = 0;

        // UI In-game
        ProgressBar pbHealth, pbXp;
        Label lblScore, lblLevel, lblCountdown;
        PictureBox player;

        // Quản lý Object
        List<PictureBox> playerBullets = new List<PictureBox>();
        List<PictureBox> enemyBullets = new List<PictureBox>();
        List<PictureBox> enemies = new List<PictureBox>();
        Random rnd = new Random();

        string saveFile = "savegame.txt";
        string leaderboardFile = "leaderboard.txt";

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
            this.Text = "Space Shooter - Advanced";
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
            
            Label title = new Label { Text = "SPACE DEFENDER", ForeColor = Color.Cyan, Font = new Font("Arial", 30, FontStyle.Bold), AutoSize = true, Location = new Point(100, 100) };
            Button btnNewGame = CreateButton("Chơi Mới", 200, 300, (s, e) => StartCountdown(false));
            Button btnContinue = CreateButton("Chơi Tiếp", 200, 380, (s, e) => StartCountdown(true));
            Button btnLeaderboard = CreateButton("Bảng Xếp Hạng", 200, 460, (s, e) => ShowLeaderboard());

            pnlMenu.Controls.AddRange(new Control[] { title, btnNewGame, btnContinue, btnLeaderboard });
            this.Controls.Add(pnlMenu);
        }

        private void SetupGamePanel()
        {
            pnlGame = new Panel { Dock = DockStyle.Fill, Visible = false };
            
            pbHealth = new ProgressBar { Value = 100, Maximum = 100, Width = 200, Height = 20, Location = new Point(10, 10), ForeColor = Color.Red };
            pbXp = new ProgressBar { Value = 0, Maximum = 50, Width = 200, Height = 10, Location = new Point(10, 35), ForeColor = Color.Blue };
            
            lblScore = new Label { Text = "Điểm: 0", ForeColor = Color.White, Font = new Font("Arial", 14), Location = new Point(450, 10), AutoSize = true };
            lblLevel = new Label { Text = "Lv: 1", ForeColor = Color.Yellow, Font = new Font("Arial", 14), Location = new Point(220, 10), AutoSize = true };
            lblCountdown = new Label { Text = "3", ForeColor = Color.White, Font = new Font("Arial", 72, FontStyle.Bold), AutoSize = true, Visible = false };
            
            player = new PictureBox { Size = new Size(60, 60), BackColor = Color.Transparent, SizeMode = PictureBoxSizeMode.StretchImage };
            LoadImageSafe(player, "Assets/player.png", Color.DodgerBlue);

            pnlGame.Controls.AddRange(new Control[] { pbHealth, pbXp, lblScore, lblLevel, lblCountdown, player });
            this.Controls.Add(pnlGame);

            gameTimer = new Timer { Interval = 20 };
            gameTimer.Tick += GameLoop;

            countdownTimer = new Timer { Interval = 1000 };
            countdownTimer.Tick += CountdownTick;
        }

        private void SetupUpgradePanel()
        {
            pnlUpgrade = new Panel { Size = new Size(400, 300), Location = new Point(100, 200), BackColor = Color.FromArgb(50, 50, 70), Visible = false };
            Label lblUp = new Label { Text = "LÊN CẤP! CHỌN NÂNG CẤP", ForeColor = Color.Gold, Font = new Font("Arial", 16, FontStyle.Bold), Location = new Point(40, 20), AutoSize = true };
            
            Button btnHeal = CreateButton("Hồi 50% Máu", 100, 80, (s, e) => ApplyUpgrade(1));
            Button btnSpeed = CreateButton("Tăng Tốc Độ Bắn", 100, 150, (s, e) => ApplyUpgrade(2));
            Button btnDmg = CreateButton("Đạn Bay Nhanh Hơn", 100, 220, (s, e) => ApplyUpgrade(3));

            pnlUpgrade.Controls.AddRange(new Control[] { lblUp, btnHeal, btnSpeed, btnDmg });
            this.Controls.Add(pnlUpgrade);
            pnlUpgrade.BringToFront();
        }

        private Button CreateButton(string text, int x, int y, EventHandler clickEvent)
        {
            Button btn = new Button { Text = text, Location = new Point(x, y), Size = new Size(200, 50), Font = new Font("Arial", 12, FontStyle.Bold), BackColor = Color.White };
            btn.Click += clickEvent;
            return btn;
        }

        // ================= LOGIC CHUYỂN MÀN =================
        private void ShowMenu()
        {
            pnlMenu.Visible = true;
            pnlGame.Visible = false;
            pnlUpgrade.Visible = false;
            gameTimer.Stop();
        }

        private void StartCountdown(bool isLoad)
        {
            if (isLoad && File.Exists(saveFile)) LoadGame();
            else ResetGameStats();

            pnlMenu.Visible = false;
            pnlGame.Visible = true;
            lblCountdown.Visible = true;
            lblCountdown.Location = new Point(this.ClientSize.Width / 2 - 40, this.ClientSize.Height / 2 - 50);
            
            countdownSeconds = 3;
            lblCountdown.Text = countdownSeconds.ToString();
            countdownTimer.Start();

            // THÊM DÒNG NÀY ĐỂ GIẬT LẠI FOCUS TỪ NÚT BẤM VỀ MÀN HÌNH CHÍNH
            this.Focus();
        }

        private void CountdownTick(object sender, EventArgs e)
        {
            countdownSeconds--;
            if (countdownSeconds > 0)
            {
                lblCountdown.Text = countdownSeconds.ToString();
            }
            else
            {
                lblCountdown.Visible = false;
                countdownTimer.Stop();
                gameTimer.Start();
            }
        }

        // ================= GAME LOOP CHÍNH =================
        private void GameLoop(object sender, EventArgs e)
        {
            // 1. Di chuyển người chơi
            if (goLeft && player.Left > 0) player.Left -= playerSpeed;
            if (goRight && player.Left < pnlGame.Width - player.Width) player.Left += playerSpeed;

            // 2. Cooldown bắn súng
            if (fireCooldown > 0) fireCooldown--;
            if (isShooting && fireCooldown == 0)
            {
                ShootPlayerBullet();
                fireCooldown = fireRate;
            }

            // 3. Cập nhật Đạn người chơi
            for (int i = playerBullets.Count - 1; i >= 0; i--)
            {
                playerBullets[i].Top -= bulletSpeed;
                if (playerBullets[i].Top < 0) RemoveControl(playerBullets[i], playerBullets);
            }

            // 4. Sinh và Cập nhật Kẻ địch
            SpawnEnemies();
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var en = enemies[i];
                en.Top += (int)en.Tag; // Tốc độ cố định lưu trong Tag
                
                // Địch bắn trả (2% cơ hội mỗi frame)
                if (rnd.Next(0, 100) < 2) ShootEnemyBullet(en);

                // Va chạm Người - Địch
                if (player.Bounds.IntersectsWith(en.Bounds))
                {
                    TakeDamage(20);
                    RemoveControl(en, enemies);
                }
                else if (en.Top > pnlGame.Height) RemoveControl(en, enemies);
            }

            // 5. Cập nhật Đạn kẻ địch
            for (int i = enemyBullets.Count - 1; i >= 0; i--)
            {
                enemyBullets[i].Top += 10;
                if (player.Bounds.IntersectsWith(enemyBullets[i].Bounds))
                {
                    TakeDamage(10);
                    RemoveControl(enemyBullets[i], enemyBullets);
                }
                else if (enemyBullets[i].Top > pnlGame.Height) RemoveControl(enemyBullets[i], enemyBullets);
            }

            // 6. Kiểm tra va chạm Đạn người - Địch
            CheckBulletCollisions();
            UpdateUI();
        }

        // ================= HÀNH ĐỘNG IN-GAME =================
        private void SpawnEnemies()
        {
            if (rnd.Next(0, 1000) < 15) 
            {
                PictureBox en = new PictureBox { Size = new Size(50, 50), Location = new Point(rnd.Next(0, 500), -50), BackColor = Color.Transparent, SizeMode = PictureBoxSizeMode.StretchImage };
                LoadImageSafe(en, "Assets/enemy.png", Color.Crimson);
                en.Tag = 5; // Tốc độ
                enemies.Add(en);
                pnlGame.Controls.Add(en);
            }
            
            // Giảm tỉ lệ ra Boss xuống 0.2% (Khoảng 10 giây mới xuất hiện 1 con)
            if (rnd.Next(0, 1000) < 2) 
            {
                PictureBox boss = new PictureBox { Size = new Size(120, 120), Location = new Point(rnd.Next(0, 400), -120), BackColor = Color.Transparent, SizeMode = PictureBoxSizeMode.StretchImage };
                LoadImageSafe(boss, "Assets/boss.png", Color.Purple);
                boss.Tag = 2; // Boss đi chậm
                boss.Name = "boss"; // Đánh dấu là boss
                enemies.Add(boss);
                pnlGame.Controls.Add(boss);
            }
        }

        private void ShootPlayerBullet()
        {
            PictureBox b = new PictureBox { Size = new Size(8, 20), Location = new Point(player.Left + player.Width / 2 - 4, player.Top - 20), BackColor = Color.Yellow };
            LoadImageSafe(b, "Assets/bullet.png", Color.Yellow);
            playerBullets.Add(b);
            pnlGame.Controls.Add(b);
        }

        private void ShootEnemyBullet(PictureBox enemy)
        {
            PictureBox b = new PictureBox { Size = new Size(8, 20), Location = new Point(enemy.Left + enemy.Width / 2, enemy.Bottom), BackColor = Color.Orange };
            LoadImageSafe(b, "Assets/enemy_bullet.png", Color.Orange);
            enemyBullets.Add(b);
            pnlGame.Controls.Add(b);
        }

        private void CheckBulletCollisions()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                for (int j = playerBullets.Count - 1; j >= 0; j--)
                {
                    if (i < enemies.Count && j < playerBullets.Count && playerBullets[j].Bounds.IntersectsWith(enemies[i].Bounds))
                    {
                        bool isBoss = enemies[i].Name == "boss";
                        RemoveControl(playerBullets[j], playerBullets);
                        RemoveControl(enemies[i], enemies);
                        
                        score += isBoss ? 50 : 10;
                        GainXp(isBoss ? 20 : 5);
                        break;
                    }
                }
            }
        }

        private void TakeDamage(int dmg)
        {
            currentHp -= dmg;
            if (currentHp <= 0)
            {
                gameTimer.Stop();
                SaveScoreToLeaderboard();
                MessageBox.Show($"GAME OVER!\nĐiểm của bạn: {score}", "Thất bại");
                ShowMenu();
            }
        }

        private void GainXp(int amount)
        {
            currentXp += amount;
            if (currentXp >= xpToNextLevel)
            {
                currentLevel++;
                currentXp -= xpToNextLevel;
                xpToNextLevel += 20; // Tăng yêu cầu XP cho cấp sau
                TriggerLevelUp();
            }
        }

        private void TriggerLevelUp()
        {
            gameTimer.Stop();
            goLeft = false; goRight = false; isShooting = false;
            pnlUpgrade.Visible = true;
            pnlUpgrade.BringToFront();
        }

        private void ApplyUpgrade(int type)
        {
            if (type == 1) currentHp = Math.Min(maxHp, currentHp + 50);
            if (type == 2) fireRate = Math.Max(3, fireRate - 3);
            if (type == 3) bulletSpeed += 5;

            pnlUpgrade.Visible = false;
            UpdateUI();
            
            // Tiếp tục lưu đệm 3s trước khi chơi lại
            lblCountdown.Visible = true;
            countdownSeconds = 3;
            lblCountdown.Text = "3";
            countdownTimer.Start();
            this.Focus();
        }

        // ================= TIỆN ÍCH & HỆ THỐNG =================
        private void UpdateUI()
        {
            lblScore.Text = $"Điểm: {score}";
            lblLevel.Text = $"Lv: {currentLevel}";
            pbHealth.Value = Math.Max(0, currentHp);
            pbXp.Maximum = xpToNextLevel;
            pbXp.Value = Math.Min(currentXp, xpToNextLevel);
        }

        private void ResetGameStats()
        {
            currentHp = maxHp; score = 0; currentLevel = 1; currentXp = 0; xpToNextLevel = 50; fireRate = 15; bulletSpeed = 20;
            player.Location = new Point(270, 700);
            ClearAllEntities();
            UpdateUI();
        }

        private void ClearAllEntities()
        {
            foreach (var b in playerBullets) pnlGame.Controls.Remove(b);
            foreach (var b in enemyBullets) pnlGame.Controls.Remove(b);
            foreach (var e in enemies) pnlGame.Controls.Remove(e);
            playerBullets.Clear(); enemyBullets.Clear(); enemies.Clear();
        }

        private void RemoveControl(PictureBox pb, List<PictureBox> list)
        {
            pnlGame.Controls.Remove(pb);
            list.Remove(pb);
            pb.Dispose();
        }

        private void LoadImageSafe(PictureBox pb, string path, Color fallback)
        {
            try { pb.Image = Image.FromFile(path); pb.BackColor = Color.Transparent; }
            catch { pb.BackColor = fallback; }
        }

        // ================= INPUT & SAVE/LOAD =================
        private void KeyIsDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) goLeft = true;
            if (e.KeyCode == Keys.Right) goRight = true;
            if (e.KeyCode == Keys.Space) isShooting = true;
            if (e.KeyCode == Keys.Escape && pnlGame.Visible && !lblCountdown.Visible) // Pause & Save
            {
                gameTimer.Stop();
                SaveGame();
                ShowMenu();
            }
        }

        private void KeyIsUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) goLeft = false;
            if (e.KeyCode == Keys.Right) goRight = false;
            if (e.KeyCode == Keys.Space) isShooting = false;
        }

        private void SaveGame()
        {
            string data = $"{score},{currentLevel},{currentHp},{currentXp},{xpToNextLevel},{fireRate},{bulletSpeed}";
            File.WriteAllText(saveFile, data);
            MessageBox.Show("Đã lưu tiến trình chơi!", "Thông báo");
        }

        private void LoadGame()
        {
            try
            {
                string[] data = File.ReadAllText(saveFile).Split(',');
                score = int.Parse(data[0]); currentLevel = int.Parse(data[1]); currentHp = int.Parse(data[2]);
                currentXp = int.Parse(data[3]); xpToNextLevel = int.Parse(data[4]); fireRate = int.Parse(data[5]); bulletSpeed = int.Parse(data[6]);
                ClearAllEntities(); UpdateUI();
            }
            catch { ResetGameStats(); }
        }

        private void SaveScoreToLeaderboard()
        {
            File.AppendAllText(leaderboardFile, $"{DateTime.Now:dd/MM/yyyy HH:mm} - Điểm: {score}\n");
        }

        private void ShowLeaderboard()
        {
            string msg = File.Exists(leaderboardFile) ? File.ReadAllText(leaderboardFile) : "Chưa có dữ liệu.";
            MessageBox.Show(msg, "Bảng Xếp Hạng");
        }
    }
}