using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace CodeLineHelper {
	/// <summary>
	/// MainWindow.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class MainWindow : Window {
		public bool IsRunning {
			get; private set;
		}

		private List<Text> contentList = new List<Text>();
		private List<Text> commentList = new List<Text>();
		private Text emptyText;
		private int line;

		//Info
		private DirectoryInfo dirInfo;
		private string ext;

		//UI Progress
		private int stage;
		private bool isInspecting;
		FolderBrowserDialog dialog;
		//Comment
		private PlanText currentPlan;
		//Content
		private Text dirPath;
		private Text resultText;

		//Core
		private Queue<Action> actionQueue = new Queue<Action>();

		public MainWindow() {
			InitializeComponent();
			Init();
			SetEvent();
		}
		private void Init() {
			//라인 넘버
			emptyText = new Text();
			StringBuilder builder = new StringBuilder();
			for (int i = 1; i < 50; i++) {
				builder.AppendLine(i.ToString());
			}
			LineNumText.Content = builder.ToString();
			builder.Clear();

			//변수 생성
			dirPath = new Text();
			resultText = new Text();
			dialog = new FolderBrowserDialog();
			dialog.ShowNewFolderButton = false;

			Clear();

			Log1();

			StartCore();
		}
		private void SetEvent() {
			Closing += OnDestroy;
			ExplorerBtn.Click += OnExplorerBtnClick;
			ExtApplyBtn.Click += OnExtApplyBtnClick;
			ExtText.KeyDown += OnExtTextKeyDown;
		}

		private void OnDestroy(object sender, System.ComponentModel.CancelEventArgs e) {
			IsRunning = false;
		}


		private void OnExplorerBtnClick(object sender, RoutedEventArgs e) {
			if (isInspecting)
				return;

			DialogResult result = dialog.ShowDialog();

			if (result == System.Windows.Forms.DialogResult.OK) {

				dirInfo = new DirectoryInfo(dialog.SelectedPath);
				dirPath.text = "선택된 경로 : " + dirInfo.ToString();

				if (stage < 2) {
					currentPlan.Complete();
					AddLine(3);
					AddContent(dirPath);
					AddLine();

					Log2();
				}
				RenderText();
			}
		}
		private void OnExtTextKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
			if (e.Key == System.Windows.Input.Key.Enter) {
				OnExtInputComplete();
				e.Handled = true;
			}
		}
		private void OnExtApplyBtnClick(object sender, RoutedEventArgs e) {
			OnExtInputComplete();
		}
		private void OnExtInputComplete() {
			if (isInspecting)
				return;

			ext = ExtText.Text;

			if (stage < 3) {
				currentPlan.Complete();
				AddLine(3);
				AddContent(new Text("정보가 모두 입력되었습니다."));
				AddLine();
				AddContent(resultText);
			}

			FindLineCount();
		}

		//Chapter
		private void Log1() {
			++stage;

			AddComment(new PlanText("라인을 셀 프로젝트 폴더를 지정하시오."));
			ExplorerBtn.Visibility = Visibility.Visible;
		}
		private void Log2() {
			++stage;
			AddComment(new PlanText("검사할 파일 확장자를 입력하시오."));
			ExtInputContext.Visibility = Visibility.Visible;
		}
		private async void FindLineCount() {
			++stage;
			int lineCount = 0;

			if (string.IsNullOrEmpty(ext)) {
				resultText.text = "정상적인 확장자를 입력하십시오.";
				RenderContent();
				return;
			}

			isInspecting = true;

			resultText.text = "파일 정보를 수집하는 중…";

			RenderContent();

			try {
				if (ext[0] != '.') {
					ext = "." + ext;
				}
				ext = "*" + ext;

				FileInfo[] files = null;

				Task task = Task.Factory.StartNew(FindTask);
				await task;

				void FindTask() {
					files = dirInfo.GetFiles(ext, SearchOption.AllDirectories);


					for (int i = 0; i < files.Length; i++) {
						lineCount += GetLine(files[i]);

						if (i % 5 == 0) {
							AddQueue(() => {
								resultText.text = ((int)((float)i / files.Length * 100f)) + "% 검사 완료";
								RenderContent();
							});
						}
					}
				}

				string lineCountText = lineCount.ToString("N");
				lineCountText = lineCountText.Substring(0, lineCountText.Length - 3);
				string lineCountPerFileText = (lineCount / files.Length).ToString("N");
				lineCountPerFileText = lineCountPerFileText.Substring(0, lineCountPerFileText.Length - 3);

				AddQueue(() => {
					resultText.text =
						"파일 수 : " + files.Length + " 개\n" +
						"라인 수 : " + lineCountText + " 개\n" +
						"파일 당 라인수 : " + lineCountPerFileText + "개";
					RenderContent();
				});
			} catch {
				AddQueue(() => {
					resultText.text = "라인을 찾는 도중 오류가 발생했습니다.";
					RenderContent();
				});
			}

			isInspecting = false;

			
		}
		private int GetLine(FileInfo file) {
			string data;
			using (FileStream fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read)) {
				using (StreamReader reader = new StreamReader(fileStream)) {
					data = reader.ReadToEnd();
				}
			}
			return data.Count(t => t == '\n') + 1;
		}


		private void RenderText() {
			RenderContent();
			RenderComment();
		}
		private void RenderContent() {
			StringBuilder builder = new StringBuilder();
			int loopCount = contentList.Count;
			for (int i = 0; i < loopCount; i++) {
				builder.AppendLine(contentList[i].ToString());
			}
			ContentText.Content = builder.ToString();
		}
		private void RenderComment() {
			StringBuilder builder = new StringBuilder();
			int loopCount = commentList.Count;
			for (int i = 0; i < loopCount; i++) {
				Text text = commentList[i];
				builder.AppendLine(text.ToString());
			}
			CommentText.Content = builder.ToString();
		}

		private void AddContent(Text text) {
			contentList.Add(text);
			RenderContent();
		}
		private void AddComment(Text text) {
			if (text is PlanText) {
				currentPlan = text as PlanText;
			}
			commentList.Add(text);
			RenderComment();
		}
		private void Clear() {
			stage = 0;
			contentList.Clear();
			commentList.Clear();
			RenderText();

			ExplorerBtn.Visibility = Visibility.Collapsed;
			ExtInputContext.Visibility = Visibility.Collapsed;
			ExtText.Text = "";
		}
		private void AddLine() {
			contentList.Add(emptyText);
			commentList.Add(emptyText);
			++line;
		}
		private void AddLine(int count) {
			for (int i = 0; i < count; i++) {
				AddLine();
			}
		}

		//Core
		private async void StartCore() {
			const int FPS = 60;
			IsRunning = true;
			for (; ; ) {
				if (!IsRunning)
					return;

				Action action;
				while (actionQueue.Count > 0) {
					try {
						lock (actionQueue) {
							action = actionQueue.Dequeue();
						}
						action();
					} catch {

					}
				}

				await Task.Delay(FPS);
			}
		}
		private void AddQueue(Action action) {
			lock (actionQueue) {
				actionQueue.Enqueue(action);
			}
		}
	}
	public class Text {
		public string text;

		public Text() {
			text = "";
		}
		public Text(string text) {
			this.text = text;
		}
		public override string ToString() {
			return text.ToString();
		}
	}
	public class PlanText : Text {
		public bool isComplete;

		public PlanText() : base() {
		}
		public PlanText(string text) : base(text) {
		}
		public void Complete() {
			isComplete = true;
		}
		public override string ToString() {
			string header = "//";
			header += isComplete ? "Complete : " : "TODO : ";
			return header + text;
		}
	}
}
