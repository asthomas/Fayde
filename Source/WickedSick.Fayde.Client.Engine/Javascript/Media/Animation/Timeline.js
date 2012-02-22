/// <reference path="../../Runtime/RefObject.js" />
/// <reference path="../../Core/DependencyObject.js"/>
/// CODE
/// <reference path="../../Primitives/Duration.js"/>

//#region Timeline

function Timeline() {
    DependencyObject.call(this);
    if (!IsDocumentReady())
        return;
    this.Completed = new MulticastEvent();
    this.Reset();
}
Timeline.InheritFrom(DependencyObject);

//#region DEPENDENCY PROPERTIES

Timeline.BeginTimeProperty = DependencyProperty.Register("BeginTime", function () { return TimeSpan; }, Timeline);
Timeline.prototype.GetBeginTime = function () {
    ///<returns type="TimeSpan"></returns>
    return this.GetValue(Timeline.BeginTimeProperty);
};
Timeline.prototype.SetBeginTime = function (value) {
    ///<param name="value" type="TimeSpan"></param>
    this.SetValue(Timeline.BeginTimeProperty, value);
};

Timeline.DurationProperty = DependencyProperty.Register("Duration", function () { return Duration; }, Timeline);
Timeline.prototype.GetDuration = function () {
    ///<returns type="Duration"></returns>
    return this.GetValue(Timeline.DurationProperty);
};
Timeline.prototype.SetDuration = function (value) {
    ///<param name="value" type="Duration"></param>
    this.SetValue(Timeline.DurationProperty, value);
};

//#endregion

Timeline.prototype.HasManualTarget = function () {
    return this._ManualTarget != null;
};
Timeline.prototype.GetManualTarget = function () {
    return this._ManualTarget;
};

Timeline.prototype.Reset = function () {
    this._IsFirstUpdate = true;
    this._BeginStep = null;
    this._HasReachedBeg = false;
};
Timeline.prototype.IsAfterBeginTime = function (nowTime) {
    var beginTime = this.GetBeginTime();
    if (beginTime == null || beginTime.IsZero())
        return true;
    var ts = new TimeSpan();
    ts.AddMilliseconds(nowTime - this._InitialStep);
    if (ts.CompareTo(beginTime) < 0)
        return false;
    return true;
};
Timeline.prototype.HasDurationElapsed = function (nowTime) {
    var duration = this.GetDuration();
    if (duration == null)
        return true;
    if (!duration.HasTimeSpan())
        return false;
    if (this.GetCurrentProgress(nowTime) < 1.0)
        return false;
    return true;
};
Timeline.prototype.GetCurrentProgress = function (nowTime) {
    if (nowTime === Number.POSITIVE_INFINITY)
        return 1.0;
    var elapsedMs = nowTime - this._BeginStep;
    var progress = elapsedMs / this.GetDuration().GetTimeSpan().GetMilliseconds();
    if (progress > 1.0)
        progress = 1.0;
    return progress;
};

Timeline.prototype.Update = function (nowTime) {
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
        if (this.HasDurationElapsed(nowTime)) {
            this.UpdateInternal(Number.POSITIVE_INFINITY);
            this.OnDurationReached();
            return;
        }
        this.UpdateInternal(nowTime);
    } finally {
        this._LastStep = nowTime;
    }
};
Timeline.prototype.UpdateInternal = function (nowTime) { };
Timeline.prototype.OnDurationReached = function () {
    this.Completed.Raise(this, {});
};

//#endregion
