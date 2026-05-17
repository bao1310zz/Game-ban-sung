using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace SpaceShooter
{
    public class EnemyInfo { public int Hp; public int MaxHp; public int Speed; public bool IsBoss; public int FireCooldown; }
    public class BulletInfo { public int Dx; public int Dy; public int Dmg; }

    public partial class Form1 : Form
    {
        Panel pnlMenu, pnlGame, pnlUpgrade;
        Timer gameTimer, countdownTimer;
        int countdownSeconds = 3;
        bool goLeft, goRight, goUp, goDown, isShooting;
        
        // CƠ CHẾ AUTO-FIRE (NÚT A)
        bool autoFire = false;
        bool aKeyPressed = false; // Ngăn chặn việc đè phím A nháy liên tục

        // --- CHỈ SỐ NGƯỜI CHƠI ---
        int playerSpeed = 10, bulletSpeed = 20, fireRate = 15;
        int fireCooldown = 0;
        int maxHp = 100, currentHp = 100;
        int currentXp = 0, xpToNextLevel = 50, currentLevel = 1;
        int score = 0;
        
        // Vũ khí ĐÃ ĐƯỢC SỬA: Cộng dồn vô hạn
        int multiShot = 1; 
        int bigBulletLevel = 0; 

        // --- HỆ THỐNG WAVE (ĐỢT) ---
        int currentWave = 1;
        int enemiesToSpawn = 3; 
        int enemiesSpawned = 0;
        int wavePauseTimer = 0; 

        ProgressBar pbHealth, pbXp;
        Label lblScore, lblLevel, lblCountdown, lblWave, lblAutoFire;
        PictureBox player;

        List<PictureBox> playerBullets = new List<PictureBox>();
        List<PictureBox> enemyBullets = new List<PictureBox>();
        List<PictureBox> enemies = new List<PictureBox>();
        Random rnd = new Random();

        Button[] btnUpgrades = new Button[3]; 

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
            lblAutoFire = new Label { Text = "Auto-Fire: OFF (Phím A)", ForeColor = Color.Gray, Font = new Font("Arial", 10), Location = new Point(10, 50), AutoSize = true };
            lblCountdown = new Label { Text = "3", ForeColor = Color.White, Font = new Font("Arial", 72, FontStyle.Bold), AutoSize = true, Visible = false };
            
            player = new PictureBox { Size = new Size(60, 60), BackColor = Color.Transparent, SizeMode = PictureBoxSizeMode.StretchImage };
            LoadImageSafe(player, "Assets/player.png", Color.DodgerBlue);

            pnlGame.Controls.AddRange(new Control[] { pbHealth, pbXp, lblScore, lblLevel, lblWave, lblAutoFire, lblCountdown, player });
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
                btnUpgrades[i] = CreateButton("Option", 50, 80 + (i * 80), UpgradeButtonClicked);
                btnUpgrades[i].Width = 400;
                pnlUpgrade.Controls.Add(btnUpgrades[i]);
            }
            this.Controls.Add(pnlUpgrade);
        }

        private void UpgradeButtonClicked(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Tag != null) ApplyUpgrade((int)btn.Tag);
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
            if (goLeft && player.Left > 0) player.Left -= playerSpeed;
            if (goRight && player.Left < pnlGame.Width - player.Width) player.Left += playerSpeed;
            if (goUp && player.Top > 0) player.Top -= playerSpeed;
            if (goDown && player.Bottom < pnlGame.Height) player.Top += playerSpeed;

            if (fireCooldown > 0) fireCooldown--;
            // ĐÃ THÊM: Điều kiện bắn tự động (autoFire)
            if ((isShooting || autoFire) && fireCooldown <= 0) 
            {
                ShootPlayerBullet();
                fireCooldown = fireRate;
            }

            for (int i = playerBullets.Count - 1; i >= 0; i--)
            {
                BulletInfo bi = (BulletInfo)playerBullets[i].Tag;
                playerBullets[i].Top += bi.Dy;
                playerBullets[i].Left += bi.Dx;
                if (playerBullets[i].Top < 0 || playerBullets[i].Left < 0 || playerBullets[i].Right > pnlGame.Width) 
                    RemoveControl(playerBullets[i], playerBullets);
            }

            HandleWaveSystem();

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var en = enemies[i];
                EnemyInfo info = (EnemyInfo)en.Tag;
                
                // ĐÃ SỬA: Boss dừng ở trên cùng xả đạn
                if (info.IsBoss && en.Top >= 30) 
                {
                    en.Top = 30; // Boss đứng lại
                }
                else 
                {
                    en.Top += info.Speed;
                }
                
                if (info.FireCooldown > 0) info.FireCooldown--;
                
                if (info.FireCooldown <= 0)
                {
                    if (info.IsBoss && en.Top == 30) // Boss chỉ xả đạn khi đã vào đúng vị trí
                    {
                        ShootEnemyBullet(en, true); 
                        // Đợt càng cao Boss bắn càng nhanh (Giới hạn tối đa siêu nhanh là 15 frames)
                        info.FireCooldown = Math.Max(15, 60 - (currentWave * 3)); 
                    }
                    else if (!info.IsBoss && rnd.Next(0, 100) < 5) 
                    {
                        ShootEnemyBullet(en, false); 
                        info.FireCooldown = Math.Max(50, 120 - (currentWave * 5)); 
                    }
                }

                if (player.Bounds.IntersectsWith(en.Bounds))
                {
                    TakeDamage(info.IsBoss ? 50 : 20); // Đụng Boss mất nửa máu
                    RemoveControl(en, enemies);
                }
                else if (en.Top > pnlGame.Height) RemoveControl(en, enemies);
            }

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

            // ĐÃ SỬA TỐI ƯU LAG: Tối đa trên màn hình chỉ có 12 con (tăng dần 1 tí theo wave nhưng không quá 15)
            int maxConcurrentEnemies = Math.Min(15, 8 + (currentWave / 2));

            if (enemiesSpawned < enemiesToSpawn)
            {
                if (enemies.Count < maxConcurrentEnemies && rnd.Next(0, 100) < 5 + currentWave) 
                {
                    SpawnEnemy(false);
                    enemiesSpawned++;
                }
                if (enemiesSpawned == enemiesToSpawn && currentWave % 2 == 0)
                {
                    SpawnEnemy(true);
                }
            }
            else if (enemies.Count == 0) // Hết đợt
            {
                currentWave++;
                enemiesToSpawn += 2; 
                enemiesSpawned = 0;
                wavePauseTimer = 100; 
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
                // ĐÃ SỬA MÁU BOSS: Máu gốc 150 + tăng 50 máu mỗi đợt. Đợt 20 máu Boss là 1150 cực trâu.
                info.Hp = 150 + (currentWave * 50); 
                info.Speed = 3; // Boss lao ra nhanh
                info.IsBoss = true;
                info.FireCooldown = 10; 
            }
            else
            {
                en.Size = new Size(50, 50);
                en.Location = new Point(rnd.Next(0, pnlGame.Width - 50), -50);
                LoadImageSafe(en, "Assets/enemy.png", Color.Crimson);
                info.Hp = 10 + (currentWave * 5); 
                info.Speed = rnd.Next(3, 7);
                info.IsBoss = false;
                info.FireCooldown = 50; 
            }
            info.MaxHp = info.Hp;
            en.Tag = info;
            enemies.Add(en);
            pnlGame.Controls.Add(en);
        }

        private void ShootPlayerBullet()
        {
            // ĐÃ SỬA: Đạn to ra liên tục theo Level
            int bSizeX = 8 + (bigBulletLevel * 5);
            int bSizeY = 20 + (bigBulletLevel * 10);
            int damage = 15 + (bigBulletLevel * 20); // Sát thương khủng khiếp hơn

            void CreateBullet(int dx, int dy, int offsetX)
            {
                PictureBox b = new PictureBox { Size = new Size(bSizeX, bSizeY), BackColor = Color.Yellow, Location = new Point(player.Left + (player.Width/2) - (bSizeX/2) + offsetX, player.Top - bSizeY) };
                LoadImageSafe(b, "Assets/bullet.png", Color.Yellow);
                b.Tag = new BulletInfo { Dx = dx, Dy = dy, Dmg = damage };
                playerBullets.Add(b);
                pnlGame.Controls.Add(b);
            }

            if (multiShot == 1) CreateBullet(0, -bulletSpeed, 0); 
            else if (multiShot == 2) { CreateBullet(-2, -bulletSpeed, -15); CreateBullet(2, -bulletSpeed, 15); } 
            else { CreateBullet(0, -bulletSpeed, 0); CreateBullet(-6, -bulletSpeed, -25); CreateBullet(6, -bulletSpeed, 25); } 
        }

        private void ShootEnemyBullet(PictureBox enemy, bool isBoss)
        {
            void CreateEnemyBullet(int dx, int dy, Color color, int size = 10)
            {
                PictureBox b = new PictureBox { Size = new Size(size, size), BackColor = color, Location = new Point(enemy.Left + enemy.Width / 2 - (size/2), enemy.Bottom) };
                LoadImageSafe(b, "Assets/enemy_bullet.png", color);
                b.Tag = new BulletInfo { Dx = dx, Dy = dy, Dmg = size > 10 ? 25 : 15 };
                enemyBullets.Add(b);
                pnlGame.Controls.Add(b);
            }

            if (isBoss)
            {
                int pattern = rnd.Next(1, 5); // ĐÃ SỬA: 4 Kiểu Đạn Khác Nhau
                
                if (pattern == 1)
                {
                    // Tỏa 5 tia dày đặc
                    CreateEnemyBullet(0, 12, Color.Orange, 12);
                    CreateEnemyBullet(-3, 10, Color.Orange, 12);
                    CreateEnemyBullet(3, 10, Color.Orange, 12);
                    CreateEnemyBullet(-6, 8, Color.Orange, 12);
                    CreateEnemyBullet(6, 8, Color.Orange, 12);
                }
                else if (pattern == 2)
                {
                    // Laser tử thần siêu to, cực nhanh
                    CreateEnemyBullet(0, 25, Color.Red, 25); 
                }
                else if (pattern == 3)
                {
                    // Đạn song nòng từ 2 cánh khóa mục tiêu
                    PictureBox b1 = new PictureBox { Size = new Size(12, 12), BackColor = Color.Cyan, Location = new Point(enemy.Left + 15, enemy.Bottom) };
                    LoadImageSafe(b1, "Assets/enemy_bullet.png", Color.Cyan);
                    b1.Tag = new BulletInfo { Dx = -2, Dy = 15, Dmg = 20 };
                    enemyBullets.Add(b1); pnlGame.Controls.Add(b1);

                    PictureBox b2 = new PictureBox { Size = new Size(12, 12), BackColor = Color.Cyan, Location = new Point(enemy.Right - 25, enemy.Bottom) };
                    LoadImageSafe(b2, "Assets/enemy_bullet.png", Color.Cyan);
                    b2.Tag = new BulletInfo { Dx = 2, Dy = 15, Dmg = 20 };
                    enemyBullets.Add(b2); pnlGame.Controls.Add(b2);
                }
                else 
                {
                    // Mưa đạn chùm ngẫu nhiên
                    CreateEnemyBullet(rnd.Next(-10, 10), rnd.Next(8, 15), Color.Magenta, 15);
                    CreateEnemyBullet(rnd.Next(-10, 10), rnd.Next(8, 15), Color.Magenta, 15);
                }
            }
            else
            {
                CreateEnemyBullet(0, 10, Color.Orange);
            }
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
                        
                        eInfo.Hp -= bInfo.Dmg; 
                        RemoveControl(playerBullets[j], playerBullets); 

                        if (eInfo.Hp <= 0) 
                        {
                            score += eInfo.IsBoss ? 100 : 10;
                            GainXp(eInfo.IsBoss ? 30 : 20); 
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
            
            var allOptions = new List<Tuple<int, string>> {
                Tuple.Create(1, "Hồi 50 HP"),
                Tuple.Create(2, "Tăng Tốc Độ Bắn"),
                Tuple.Create(3, "Tăng Tốc Độ Đạn"),
                Tuple.Create(4, multiShot < 3 ? "Thêm Nòng Súng (+1)" : $"Nâng Cấp Đạn Khổng Lồ (Lv {bigBulletLevel + 1})"),
                Tuple.Create(5, "Tăng 50 Máu Tối Đa"),
                Tuple.Create(6, "Tăng Tốc Độ Di Chuyển")
            };

            var randomOptions = allOptions.OrderBy(x => rnd.Next()).Take(3).ToList();

            for (int i = 0; i < 3; i++)
            {
                btnUpgrades[i].Text = randomOptions[i].Item2;
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
                    else bigBulletLevel++; // ĐÃ SỬA: Cộng dồn level đạn vô hạn
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
            lblAutoFire.Text = autoFire ? "Auto-Fire: ON (Phím A)" : "Auto-Fire: OFF (Phím A)";
            lblAutoFire.ForeColor = autoFire ? Color.Lime : Color.Gray;
        }

        private void ResetGameStats() {
            currentHp = 100; maxHp = 100; score = 0; currentLevel = 1; currentXp = 0; xpToNextLevel = 50; 
            fireRate = 15; bulletSpeed = 20; playerSpeed = 10; multiShot = 1; bigBulletLevel = 0; autoFire = false;
            currentWave = 1; enemiesSpawned = 0; enemiesToSpawn = 3; 
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
            if (e.KeyCode == Keys.Left) goLeft = true;
            if (e.KeyCode == Keys.Right) goRight = true;
            if (e.KeyCode == Keys.Up) goUp = true;
            if (e.KeyCode == Keys.Down) goDown = true;
            if (e.KeyCode == Keys.Space) isShooting = true;
            
            // XỬ LÝ AUTO FIRE: Nhấn A để bật/tắt
            if (e.KeyCode == Keys.A && !aKeyPressed) 
            {
                autoFire = !autoFire;
                aKeyPressed = true;
                UpdateUI();
            }
        }
        
        private void KeyIsUp(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Left) goLeft = false;
            if (e.KeyCode == Keys.Right) goRight = false;
            if (e.KeyCode == Keys.Up) goUp = false;
            if (e.KeyCode == Keys.Down) goDown = false;
            if (e.KeyCode == Keys.Space) isShooting = false;
            
            // Thả phím A ra để chuẩn bị cho lần bấm tiếp theo
            if (e.KeyCode == Keys.A) aKeyPressed = false;
        }
    }
}