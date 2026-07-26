using NUnit.Framework;
using TomeOfTongues.Application.Learning;
using TomeOfTongues.Core.Curriculum;
using TomeOfTongues.Core.Exercises;
using TomeOfTongues.Core.LearningSessions;
using TomeOfTongues.Core.Progress;

namespace TomeOfTongues.Application.Tests;

[TestFixture]
public sealed class LearningUseCaseTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 7, 26, 8, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Start_then_resume_round_trips_through_repository_snapshots()
    {
        var unitOfWork = new FakeLearningUnitOfWork();
        var start = new StartOrResumeLessonUseCase(unitOfWork);

        var created = await start.ExecuteAsync(StartCommand("session-1", "lesson-1", StartedAt));
        var resumed = await new StartOrResumeLessonUseCase(unitOfWork).ExecuteAsync(
            StartCommand("unused-session", "lesson-1", StartedAt.AddMinutes(1)));

        Assert.Multiple(() =>
        {
            Assert.That(created.WasResumed, Is.False);
            Assert.That(resumed.WasResumed, Is.True);
            Assert.That(resumed.Session.SessionId, Is.EqualTo(created.Session.SessionId));
            Assert.That(resumed.Session.Mode.IsSilent, Is.True);
            Assert.That(unitOfWork.SessionSnapshots, Has.Count.EqualTo(1));
            Assert.That(unitOfWork.CommitCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void Start_rejects_a_locked_lesson_without_persisting_enrollment()
    {
        var unitOfWork = new FakeLearningUnitOfWork();
        var start = new StartOrResumeLessonUseCase(unitOfWork);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await start.ExecuteAsync(
                StartCommand("session-locked", "lesson-2", StartedAt)));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("not unlocked"));
            Assert.That(unitOfWork.SessionSnapshots, Is.Empty);
            Assert.That(unitOfWork.ProgressSnapshot, Is.Null);
            Assert.That(unitOfWork.CommitCount, Is.Zero);
            Assert.That(unitOfWork.RollbackCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Evaluated_response_persists_attempt_evidence_and_next_position()
    {
        var unitOfWork = new FakeLearningUnitOfWork();
        await StartFirstLessonAsync(unitOfWork);
        var submit = new SubmitLearningStepUseCase(unitOfWork);

        var result = await submit.ExecuteAsync(
            EvaluatedCommand("attempt-1", isCorrect: true, StartedAt.AddMinutes(1)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Attempt.Evidence, Has.Count.EqualTo(1));
            Assert.That(result.SkillStates, Has.Count.EqualTo(1));
            Assert.That(
                result.Session.CurrentStepId,
                Is.EqualTo(new StepId("speaking")));
            Assert.That(result.Progress.CompletedLessonIds, Is.Empty);
            Assert.That(
                result.Progress.LastStepId,
                Is.EqualTo(new StepId("speaking")));
            Assert.That(unitOfWork.Attempts, Has.Count.EqualTo(1));
            Assert.That(unitOfWork.SkillStateSnapshots, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Failed_script_evidence_and_deferred_speaking_do_not_gate_progress()
    {
        var unitOfWork = new FakeLearningUnitOfWork();
        await StartFirstLessonAsync(unitOfWork);
        var submit = new SubmitLearningStepUseCase(unitOfWork);

        var scriptResult = await submit.ExecuteAsync(
            EvaluatedCommand("attempt-script", isCorrect: false, StartedAt.AddMinutes(1)));
        var speakingResult = await submit.ExecuteAsync(
            new SubmitLearningStepCommand(
                new LearningSessionId("session-1"),
                new ExerciseAttemptId("attempt-speaking"),
                PackRevision: 1,
                Response: null,
                Assistance: [],
                Latency: null,
                Confidence: null,
                AttemptOutcome.Deferred,
                IsCorrect: null,
                Measurement: null,
                StartedAt.AddMinutes(2),
                StartedAt.AddMinutes(3)));

        Assert.Multiple(() =>
        {
            Assert.That(scriptResult.Attempt.Evidence.Single().IsCorrect, Is.False);
            Assert.That(speakingResult.Attempt.Evidence, Is.Empty);
            Assert.That(
                speakingResult.Session.DeferredStepIds,
                Is.EqualTo([new StepId("speaking")]));
            Assert.That(
                speakingResult.Progress.CompletedLessonIds,
                Is.EqualTo([new LessonId("lesson-1")]));
            Assert.That(
                speakingResult.Progress.UnlockedLessonIds,
                Does.Contain(new LessonId("lesson-2")));
        });
    }

    [Test]
    public async Task Repository_failure_rolls_back_all_step_mutations()
    {
        var unitOfWork = new FakeLearningUnitOfWork();
        await StartFirstLessonAsync(unitOfWork);
        unitOfWork.FailAttemptSaves = true;
        var submit = new SubmitLearningStepUseCase(unitOfWork);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await submit.ExecuteAsync(
                EvaluatedCommand("attempt-failure", isCorrect: true, StartedAt.AddMinutes(1))));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("attempt save"));
            Assert.That(unitOfWork.Attempts, Is.Empty);
            Assert.That(unitOfWork.SkillStateSnapshots, Is.Empty);
            Assert.That(
                unitOfWork.SessionSnapshots.Single().CurrentStepId,
                Is.EqualTo(new StepId("script")));
            Assert.That(
                unitOfWork.ProgressSnapshot!.CompletedLessonIds,
                Is.Empty);
            Assert.That(unitOfWork.CommitCount, Is.EqualTo(1));
            Assert.That(unitOfWork.RollbackCount, Is.EqualTo(1));
        });
    }

    private static StartOrResumeLessonCommand StartCommand(
        string sessionId,
        string lessonId,
        DateTimeOffset occurredAt) =>
        new(
            new LearningSessionId(sessionId),
            new CourseId("course"),
            new LessonId(lessonId),
            IsSilent: true,
            occurredAt);

    private static SubmitLearningStepCommand EvaluatedCommand(
        string attemptId,
        bool isCorrect,
        DateTimeOffset completedAt) =>
        new(
            new LearningSessionId("session-1"),
            new ExerciseAttemptId(attemptId),
            PackRevision: 1,
            Response: "answer",
            Assistance: [],
            Latency: new ResponseLatency(TimeSpan.FromSeconds(1)),
            Confidence: new ConfidenceRating(3),
            AttemptOutcome.Evaluated,
            isCorrect,
            EvidenceMeasurement.Measured,
            completedAt.AddSeconds(-2),
            completedAt);

    private static async Task StartFirstLessonAsync(FakeLearningUnitOfWork unitOfWork)
    {
        await new StartOrResumeLessonUseCase(unitOfWork).ExecuteAsync(
            StartCommand("session-1", "lesson-1", StartedAt));
    }

    private sealed class FakeLearningUnitOfWork : ILearningUnitOfWork
    {
        private Store _store = Store.Create();

        public bool FailAttemptSaves { get; set; }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public IReadOnlyCollection<LearningSessionSnapshot> SessionSnapshots =>
            _store.Sessions.Values;

        public LearnerProgressSnapshot? ProgressSnapshot => _store.Progress;

        public IReadOnlyList<ExerciseAttempt> Attempts => _store.Attempts;

        public IReadOnlyCollection<LearnerSkillStateSnapshot> SkillStateSnapshots =>
            _store.SkillStates.Values;

        public async Task<TResult> ExecuteAsync<TResult>(
            Func<ILearningTransaction, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            cancellationToken.ThrowIfCancellationRequested();

            var staged = _store.Clone();
            var transaction = new FakeLearningTransaction(staged, FailAttemptSaves);

            try
            {
                var result = await operation(transaction, cancellationToken);
                _store = staged;
                CommitCount++;
                return result;
            }
            catch
            {
                RollbackCount++;
                throw;
            }
        }
    }

    private sealed class FakeLearningTransaction :
        ILearningTransaction,
        ICurriculumRepository,
        ILearningSessionRepository,
        ILearnerProgressRepository,
        IExerciseAttemptRepository,
        ILearnerSkillStateRepository
    {
        private readonly Store _store;
        private readonly bool _failAttemptSaves;

        public FakeLearningTransaction(Store store, bool failAttemptSaves)
        {
            _store = store;
            _failAttemptSaves = failAttemptSaves;
        }

        public ICurriculumRepository Curriculum => this;

        public ILearningSessionRepository Sessions => this;

        public ILearnerProgressRepository Progress => this;

        public IExerciseAttemptRepository Attempts => this;

        public ILearnerSkillStateRepository SkillStates => this;

        public Task<CurriculumCourse?> GetCourseAsync(
            CourseId courseId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<CurriculumCourse?>(
                courseId == _store.Course.Id ? _store.Course : null);
        }

        public Task<ExerciseDefinition?> GetExerciseAsync(
            CourseId courseId,
            LessonId lessonId,
            StepId stepId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _store.Exercises.TryGetValue((courseId, lessonId, stepId), out var exercise);
            return Task.FromResult(exercise);
        }

        public Task<LearningSession?> GetActiveAsync(
            CourseId courseId,
            LessonId lessonId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = _store.Sessions.Values.SingleOrDefault(
                item =>
                    item.CourseId == courseId &&
                    item.LessonId == lessonId &&
                    item.Status == LearningSessionStatus.InProgress);
            return Task.FromResult(
                snapshot is null
                    ? null
                    : LearningSession.Restore(_store.Course, snapshot));
        }

        public Task<LearningSession?> GetAsync(
            LearningSessionId sessionId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                _store.Sessions.TryGetValue(sessionId, out var snapshot)
                    ? LearningSession.Restore(_store.Course, snapshot)
                    : null);
        }

        public Task SaveAsync(
            LearningSession session,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _store.Sessions[session.Id] = session.CreateSnapshot();
            return Task.CompletedTask;
        }

        public Task<LearnerProgress?> GetAsync(
            CourseId courseId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                _store.Progress is { } snapshot && snapshot.CourseId == courseId
                    ? LearnerProgress.Restore(_store.Course, snapshot)
                    : null);
        }

        public Task SaveAsync(
            LearnerProgress progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _store.Progress = progress.CreateSnapshot();
            return Task.CompletedTask;
        }

        public Task AddAsync(
            ExerciseAttempt attempt,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_failAttemptSaves)
            {
                throw new InvalidOperationException("Simulated attempt save failure.");
            }

            _store.Attempts.Add(attempt);
            return Task.CompletedTask;
        }

        public Task<LearnerSkillState?> GetAsync(
            ObjectiveId objectiveId,
            SkillDimension skillDimension,
            RepresentationId? representationId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = new SkillStateKey(objectiveId, skillDimension, representationId);
            return Task.FromResult(
                _store.SkillStates.TryGetValue(key, out var snapshot)
                    ? LearnerSkillState.Restore(snapshot)
                    : null);
        }

        public Task SaveAsync(
            LearnerSkillState state,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = new SkillStateKey(
                state.ObjectiveId,
                state.SkillDimension,
                state.RepresentationId);
            _store.SkillStates[key] = state.CreateSnapshot();
            return Task.CompletedTask;
        }
    }

    private sealed class Store
    {
        private Store(
            CurriculumCourse course,
            Dictionary<(CourseId, LessonId, StepId), ExerciseDefinition> exercises)
        {
            Course = course;
            Exercises = exercises;
        }

        public CurriculumCourse Course { get; }

        public Dictionary<(CourseId, LessonId, StepId), ExerciseDefinition> Exercises { get; }

        public Dictionary<LearningSessionId, LearningSessionSnapshot> Sessions { get; } = [];

        public LearnerProgressSnapshot? Progress { get; set; }

        public List<ExerciseAttempt> Attempts { get; } = [];

        public Dictionary<SkillStateKey, LearnerSkillStateSnapshot> SkillStates { get; } = [];

        public static Store Create()
        {
            var scriptStep = new CurriculumStep(
                new StepId("script"),
                revision: 1,
                CurriculumStepKind.Exercise,
                CurriculumStepProgression.Required);
            var speakingStep = new CurriculumStep(
                new StepId("speaking"),
                revision: 1,
                CurriculumStepKind.OptionalSpeakingOpportunity,
                CurriculumStepProgression.DeferredAllowed);
            var firstLesson = new CurriculumLesson(
                new LessonId("lesson-1"),
                revision: 1,
                [
                    new CurriculumObjective(new ObjectiveId("read"), 1),
                    new CurriculumObjective(new ObjectiveId("speak"), 1)
                ],
                [scriptStep, speakingStep]);
            var secondLesson = new CurriculumLesson(
                new LessonId("lesson-2"),
                revision: 1,
                [new CurriculumObjective(new ObjectiveId("next"), 1)],
                [
                    new CurriculumStep(
                        new StepId("next"),
                        revision: 1,
                        CurriculumStepKind.Exercise,
                        CurriculumStepProgression.Required)
                ],
                [firstLesson.Id]);
            var course = new CurriculumCourse(
                new CourseId("course"),
                revision: 1,
                "A1",
                [new CurriculumUnit(new UnitId("unit"), 1, [firstLesson, secondLesson])]);
            var exercises =
                new Dictionary<(CourseId, LessonId, StepId), ExerciseDefinition>
                {
                    [
                        (course.Id, firstLesson.Id, scriptStep.Id)
                    ] = new ExerciseDefinition(
                        new ExerciseId("exercise-script"),
                        revision: 1,
                        PromptModality.Text,
                        ResponseModality.Typed,
                        [
                            new EvidenceMapping(
                                new ObjectiveId("read"),
                                SkillDimension.ReadingRecognition,
                                new RepresentationId("script-main"))
                        ]),
                    [
                        (course.Id, firstLesson.Id, speakingStep.Id)
                    ] = new ExerciseDefinition(
                        new ExerciseId("exercise-speaking"),
                        revision: 1,
                        PromptModality.Audio,
                        ResponseModality.Spoken,
                        [
                            new EvidenceMapping(
                                new ObjectiveId("speak"),
                                SkillDimension.SpokenProduction)
                        ])
                };

            return new Store(course, exercises);
        }

        public Store Clone()
        {
            var clone = new Store(Course, new Dictionary<(CourseId, LessonId, StepId), ExerciseDefinition>(Exercises))
            {
                Progress = Progress
            };

            foreach (var (id, snapshot) in Sessions)
            {
                clone.Sessions.Add(id, snapshot);
            }

            clone.Attempts.AddRange(Attempts);
            foreach (var (key, snapshot) in SkillStates)
            {
                clone.SkillStates.Add(key, snapshot);
            }

            return clone;
        }
    }

    private sealed record SkillStateKey(
        ObjectiveId ObjectiveId,
        SkillDimension SkillDimension,
        RepresentationId? RepresentationId);
}
