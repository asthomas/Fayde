/// <reference path="../../Runtime/Nullstone.js" />
/// <reference path="../../Core/DependencyObject.js"/>
/// CODE
/// <reference path="../../Primitives/Duration.js"/>
/// <reference path="RepeatBehavior.js"/>

//#region Timeline
var Timeline = Nullstone.Create("Timeline", DependencyObject);

Timeline.Instance.Init = function () {
    this.Init$DependencyObject();
    this.Completed = new MulticastEvent();
    this.Reset();
};

//#region Dependency Properties

Timeline.BeginTimeProperty = DependencyProperty.Register("BeginTime", function () { return TimeSpan; }, Timeline, new TimeSpan());
Timeline.DurationProperty = DependencyProperty.Register("Duration", function () { return Duration; }, Timeline, Duration.CreateAutomatic());
Timeline.RepeatBehaviorProperty = DependencyProperty.Register("RepeatBehavior", function () { return RepeatBehavior; }, Timeline, RepeatBehavior.FromIterationCount(1));

Nullstone.AutoProperties(Timeline, [
    Timeline.BeginTimeProperty,
    Timeline.DurationProperty,
    Timeline.RepeatBehaviorProperty
]);

//#endregion

Timeline.Instance.HasManualTarget = function () {
    return this._ManualTarget != null;
};
Timeline.Instance.GetManualTarget = function () {
    return this._ManualTarget;
};

Timeline.Instance.Reset = function () {
    this._IsFirstUpdate = true;
    this._BeginStep = null;
    this._HasReachedBeg = false;
};
Timeline.Instance.IsAfterBeginTime = function (nowTime) {
    var beginTime = this._GetValue(Timeline.BeginTimeProperty);
    if (beginTime == null || beginTime.IsZero())
        return true;
    var ts = new TimeSpan();
    ts.AddMilliseconds(nowTime - this._InitialStep);
    if (ts.CompareTo(beginTime) < 0)
        return false;
    return true;
};
Timeline.Instance.CreateClockData = function (nowTime) {
    var clockData = {
        BeginTicks: this._BeginStep,
        RealTicks: nowTime,
        CurrentTime: new TimeSpan(nowTime - this._BeginStep),
        Progress: 1.0,
        HasDuration: false
    };

    var duration = this._GetValue(Timeline.DurationProperty);
    if (duration != null && duration.HasTimeSpan()) {
        clockData.HasDuration = true;
        var elapsedMs = nowTime - this._BeginStep;
        var durMs = duration.GetTimeSpan().GetMilliseconds();
        if (durMs > 0) {
            clockData.Progress = elapsedMs / durMs;
            if (clockData.Progress > 1.0)
                clockData.Progress = 1.0;
        }
    }

    return clockData;
};
Timeline.Instance.OnDurationReached = function () {
    this.Completed.Raise(this, {});
};

Timeline.Instance.Update = function (nowTime) {
    try {
        if (this._IsFirstUpdate) {
            this._InitialStep = nowTime;
            this._HasReachedBeg = false;
            this._IsFirstUpdate = false;
        }
        if (!this._HasReachedBeg) {
            if (!this.IsAfterBeginTime(nowTime))
                return;
            this._BeginStep = nowTime;
            this._HasReachedBeg = true;
        }
        var clockData = this.CreateClockData(nowTime);
        if (clockData.Progress === 1.0 && clockData.HasDuration) {
            this.UpdateInternal(clockData);
            this.OnDurationReached();
            return;
        }
        this.UpdateInternal(clockData);
    } finally {
        this._LastStep = nowTime;
    }
};
Timeline.Instance.UpdateInternal = function (nowTime) { };

Nullstone.FinishCreate(Timeline);
//#endregion