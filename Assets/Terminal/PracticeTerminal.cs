using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class PracticeTerminal : MonoBehaviour
{
    public TextAsset exercises;
    public Text transcript, location, session;
    public InputField commandLine;
    public ScrollRect scroll;

    private readonly List<int> deck = new List<int>();
    private readonly List<string> history = new List<string>();
    private Exercise[] bank;
    private Exercise current;
    private bool singleQuestion;
    private int step, lastId;
    private string directory = "~", previous;
    private bool focusPending, scrollPending;
    private const string StatsKey = "comsys.terminal.stats.v1.";

    private void Start()
    {
        bank = JsonUtility.FromJson<ExerciseBank>(exercises.text).questions;
        commandLine.onSubmit.AddListener(OnSubmit);
        Write("Ubuntu 명령어 수행평가\n\nstart 로 무작위 연습을 시작하거나 solve 12 처럼 입력해 원하는 문제를 풀어보세요.\n총 " + bank.Length + "문제 · 답안은 한 줄씩 입력하세요.");
        UpdatePrompt();
        focusPending = true;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (current == null || current.steps[step] != "{end}" || keyboard == null) return;
        if ((keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) &&
            (keyboard.cKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame))
            OnSubmit("Ctrl+C");
    }

    private void LateUpdate()
    {
        if (scrollPending)
        {
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 0;
            scrollPending = false;
        }
        if (focusPending)
        {
            commandLine.text = "";
            commandLine.ActivateInputField();
            focusPending = false;
        }
    }

    private void OnSubmit(string value)
    {
        focusPending = true;
        string line = value.Trim();
        if (line.Length == 0) return;
        bool secret = current != null && current.id == 33 && step > 0;
        Write(location.text + " " + (secret ? "*********" : value));
        string[] command = line.Split((char[])null, 2, StringSplitOptions.RemoveEmptyEntries);
        if (command[0] == "solve")
        {
            SelectQuestion(command);
            return;
        }
        switch (line)
        {
            case "start":
                if (current == null) Next(); else Write("연습 중입니다. 현재 문제의 답안을 입력하세요.");
                return;
            case "stop":
                EndPractice();
                Write("연습을 멈췄습니다. start 또는 solve <문제 번호>로 다시 시작하세요.");
                return;
            case "status":
                ShowStats();
                if (current != null) ShowQuestion();
                return;
            case "giveup":
            case "next":
                if (current == null) { Write("start 또는 solve <문제 번호>로 먼저 연습을 시작하세요."); return; }
                Count("skip");
                var solution = new StringBuilder("예시 답안");
                for (int i = 0; i < current.steps.Length; i++) solution.Append("\n  " + current.Example(i));
                Write(solution.ToString());
                FinishQuestion();
                return;
        }
        if (current == null) { Write("start 로 무작위 연습, solve <문제 번호>로 문제 선택, status 로 통계를 확인할 수 있습니다."); return; }
        if (!current.Accepts(step, value, directory, previous))
        {
            Count("wrong");
            Write("오답 · " + (step+1) + "번째 줄을 다시 입력하세요.");
            return;
        }
        previous = Exercise.Normalize(line, directory);
        if (line == "cd" || line.StartsWith("cd "))
            directory = Exercise.ResolvePath(directory, line.Length == 2 ? "~" : line.Substring(3).Trim());
        if (++step == current.steps.Length)
        {
            Count("correct");
            Write("정답 · " + current.id.ToString("00") + "번 완료");
            if (current.id == 49) Write("-rw-r--r-- 1 student student 0 … testfile\n일반 파일(-) · 소유자 student는 읽기/쓰기 · 그룹 student와 기타 사용자는 읽기 (umask 022 예시)");
            FinishQuestion();
        }
        else
        {
            UpdatePrompt();
            Write(StepHint());
        }
    }

    private void SelectQuestion(string[] command)
    {
        if (command.Length != 2 || !int.TryParse(command[1], out int id))
        {
            Write("사용법 · solve <문제 번호> (예: solve 12)");
            return;
        }
        Exercise question = Array.Find(bank, item => item.id == id);
        if (question == null)
        {
            Write(id + "번 문제는 없습니다. status 로 문제 번호를 확인하세요.");
            return;
        }
        BeginQuestion(question, single: true);
    }

    private void FinishQuestion()
    {
        if (!singleQuestion)
        {
            Next();
            return;
        }
        EndPractice();
        Write("solve <문제 번호>로 다른 문제를 선택하거나 start 로 무작위 연습을 이어가세요.");
    }

    private void EndPractice()
    {
        current = null;
        singleQuestion = false;
        directory = "~";
        UpdatePrompt();
    }

    private string StepHint()
    {
        string rule = current.steps[step];
        if (rule == "{text}") return "파일 내용 한 줄을 입력하세요.";
        if (rule == "{end}") return "입력 완료 · Ctrl+C를 누르거나 Ctrl+C 라고 입력하세요.";
        if (current.id == 33 && step > 0) return step == 1 ? "새 비밀번호를 입력하세요." : "같은 비밀번호를 다시 입력하세요.";
        return "다음 명령어를 입력하세요.";
    }

    private void Next()
    {
        if (deck.Count == 0)
        {
            for (int i = 0; i < bank.Length; i++) deck.Add(i);
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0,i+1);
                (deck[i],deck[j]) = (deck[j],deck[i]);
            }
            int end = deck.Count - 1;
            if (bank[deck[end]].id == lastId && end > 0)
                (deck[0],deck[end]) = (deck[end],deck[0]);
        }
        int top = deck.Count - 1;
        Exercise question = bank[deck[top]];
        deck.RemoveAt(top);
        lastId = question.id;
        BeginQuestion(question, single: false);
    }

    private void BeginQuestion(Exercise question, bool single)
    {
        current = question;
        singleQuestion = single;
        step = 0;
        previous = "";
        directory = current.cwd;
        ShowQuestion();
        UpdatePrompt();
    }

    private void ShowQuestion()
    {
        Write("\n[" + current.id.ToString("00") + " / " + bank.Length + "]  " + current.prompt);
        if (step == 0) Write("시작 경로 " + current.cwd + "  ·  " + (current.id == 44 ? "project_106은 구성원이 없는 그룹입니다." : "문제에 필요한 실습 환경이 준비되어 있습니다."));
        else Write(StepHint());
    }

    private void UpdatePrompt()
    {
        string path = directory == "/home/student" ? "~" : directory.Replace("/home/student/", "~/");
        location.text = "student@ubuntu:" + path + "$";
        bool secret = current != null && current.id == 33 && step > 0;
        commandLine.contentType = secret ? InputField.ContentType.Password : InputField.ContentType.Standard;
        session.text = current == null ? "대기 중" : "문제 " + current.id.ToString("00") + "   /   입력 " + (step+1) + " of " + current.steps.Length;
    }

    private void Count(string kind)
    {
        string key = StatsKey + current.id + "." + kind;
        PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key) + 1);
        PlayerPrefs.Save();
    }

    private void ShowStats()
    {
        var result = new StringBuilder("문제별 누적 통계\n");
        foreach (Exercise item in bank)
        {
            string key = StatsKey + item.id + ".";
            result.AppendFormat("\n{0:00}    정답 {1}    오답 {2}    건너뜀 {3}", item.id, PlayerPrefs.GetInt(key+"correct"), PlayerPrefs.GetInt(key+"wrong"), PlayerPrefs.GetInt(key+"skip"));
        }
        Write(result.ToString());
        Write("위로 스크롤해 이전 문제의 통계를 확인하세요.");
    }

    private void Write(string entry)
    {
        history.Add(entry);
        while (history.Count > 1 && string.Join("\n", history).Length > 9000) history.RemoveAt(0);
        transcript.text = string.Join("\n", history);
        scrollPending = true;
    }
}
