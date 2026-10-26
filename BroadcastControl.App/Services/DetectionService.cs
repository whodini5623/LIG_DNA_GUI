using System;
using System.Collections.Generic;
using System.Linq;


namespace BroadcastControl.App.Services;

    public class DetectionService
    {
        private bool _hasReceivedDetectionPacket;
        private bool _hasReceivedNonEmptyDetectionPacket;
        private string? _lastFilteredOutTargetSignature;
        private readonly Dictionary<uint, DetectionPacket> _eoDetectionCache = new();
        private readonly Dictionary<uint, DetectionPacket> _irDetectionCache = new();
        private void OnEoDetectionsReceived(DetectionPacket detectionPacket)
        {
            HandleDetectionsReceived(detectionPacket, _eoDetectionCache);
        }
        private void OnIrDetectionsReceived(DetectionPacket detectionPacket)
        {
            HandleDetectionsReceived(detectionPacket, _irDetectionCache);
        }
        private void HandleDetectionsReceived(
            DetectionPacket detectionPacket,
            Dictionary<uint, DetectionPacket> detectionCache)
        {
            CacheDetectionPacket(detectionPacket, detectionCache);

            if (!_hasReceivedDetectionPacket)
            {
                _hasReceivedDetectionPacket = true;
            }

            var displayDetections = FilterDisplayDetections(detectionPacket.Detections);

            if (!_hasReceivedNonEmptyDetectionPacket && displayDetections.Count > 0)
            {
                _hasReceivedNonEmptyDetectionPacket = true;
            }

            if (detectionPacket.Detections.Count > 0 && displayDetections.Count == 0)
            {
                var filteredSignature = $"{_viewModel.SelectedPrimaryTarget}:{detectionPacket.FrameId}";
                if (!string.Equals(_lastFilteredOutTargetSignature, filteredSignature, StringComparison.Ordinal))
                {
                    _lastFilteredOutTargetSignature = filteredSignature;
                }
            }
            else
            {
                _lastFilteredOutTargetSignature = null;
            }

            NotifyDetectionAlertIfNeeded(detectionPacket.FrameId, displayDetections);
            _viewModel.UpdateDetectionSummary(displayDetections);
            RenderDetectionOverlay(forceRefresh: true);
            UpdateRiskAndMobileAlert(detectionPacket.FrameId, displayDetections);
        }
        private static void CacheDetectionPacket(DetectionPacket detectionPacket, Dictionary<uint, DetectionPacket> detectionCache)
        {
            detectionCache[detectionPacket.FrameId] = detectionPacket;
            TrimCache(detectionCache);
        }
        private bool TryGetRenderableDetectionPacket(uint currentFrameId, Dictionary<uint, DetectionPacket> detectionCache,
        out DetectionPacket detectionPacket)
        {
            if (detectionCache.TryGetValue(currentFrameId, out detectionPacket))
            {
                return true;
            }

            var recentCandidates = detectionCache
                .Where(pair => pair.Value.Detections.Count > 0 && pair.Key <= currentFrameId)
                .OrderByDescending(pair => pair.Key)
                .ToArray();

            foreach (var candidate in recentCandidates)
            {
                var frameGap = currentFrameId >= candidate.Key
                    ? currentFrameId - candidate.Key
                    : uint.MaxValue;

                if (frameGap > OverlayFrameTolerance)
                {
                    break;
                }

                detectionPacket = candidate.Value;
                return true;
            }

            var fallbackCandidates = detectionCache
                .Where(pair => pair.Value.Detections.Count > 0)
                .OrderByDescending(pair => pair.Key)
                .ToArray();

            foreach (var candidate in fallbackCandidates)
            {
                detectionPacket = candidate.Value;
                return true;
            }

            detectionPacket = default;
            return false;
        }
        private bool TryGetRenderableFrameAndDetection(out ReceivedVideoFrame frame, out DetectionPacket detectionPacket)
        {
            var isEoPrimary = _viewModel.IsEoPrimary;
            var latestFrame = isEoPrimary
                ? _viewModel.LatestEoFrame
                : _viewModel.LatestIrFrame;
            var detectionCache = isEoPrimary ? _eoDetectionCache : _irDetectionCache;

            if (latestFrame is not null &&
                detectionCache.TryGetValue(latestFrame.Value.FrameIndex, out detectionPacket))
            {
                frame = latestFrame.Value;
                return true;
            }

            var exactPairs = detectionCache
                .Where(pair => pair.Value.Detections.Count > 0)
                .OrderByDescending(pair => pair.Key)
                .ToArray();

            foreach (var pair in exactPairs)
            {
                if (!_viewModel.TryGetCachedFrame(isEoPrimary, pair.Key, out frame))
                {
                    continue;
                }

                detectionPacket = pair.Value;
                return true;
            }

            if (latestFrame is not null &&
                TryGetRenderableDetectionPacket(latestFrame.Value.FrameIndex, detectionCache, out detectionPacket))
            {
                frame = latestFrame.Value;
                return true;
            }

            frame = default;
            detectionPacket = default;
            return false;
        }

        private IReadOnlyList<DetectionInfo> FilterDisplayDetections(IReadOnlyList<DetectionInfo> detections)
        {
            var filtered = detections
                .Where(ShouldDisplayDetectionSafe)
                .ToArray();
            return filtered;
        }

        private bool ShouldDisplayDetectionSafe(DetectionInfo detection)
        {
            var className = detection.ClassName.ToLowerInvariant();
            var primaryTarget = _viewModel.SelectedPrimaryTarget;
            if (detection.Score < DisplayScoreThreshold)
            {
                return false;
            }

            if (primaryTarget == "\uBCF5\uD569")
            {
                return true;
            }

            if (primaryTarget == "\uC0AC\uB78C")
            {
                return className == "person";
            }

            if (primaryTarget == "\uACF5\uC911 \uBB34\uAE30\uCCB4\uACC4")
            {
                return className == "airplane";
            }

            if (primaryTarget == "\uC721\uC0C1 \uBB34\uAE30\uCCB4\uACC4")
            {
                return className is "bicycle" or "car" or "motorcycle" or "bus" or "truck" or "train";
            }

            if (primaryTarget == "\uD574\uC0C1 \uBB34\uAE30\uCCB4\uACC4")
            {
                return className == "boat";
            }

            if (primaryTarget == "\uD1B5\uC2E0 \uC7A5\uBE44")
            {
                return className is "cell phone" or "laptop";
            }

            if (primaryTarget == "\uBE44\uAD70\uC0AC \uD45C\uC801")
            {
                return NonMilitaryTargetClasses.Contains(className);
            }

            return true;
        }
        private void NotifyDetectionAlertIfNeeded(uint frameId, IReadOnlyList<DetectionInfo> detections)
        {
            _lastDetectionAlertSignature = detections.Count == 0 ? null : $"{frameId}:{detections.Count}";
        }

        private void UpdateRiskAndMobileAlert(uint frameId, IReadOnlyList<DetectionInfo> detections)
        {
            if (detections.Count == 0)
            {
                _viewModel.ApplyVlmAnalysisResult("낮음", "VLM 분석: 현재 선택된 주 탐지체 기준 위험 객체가 확인되지 않았습니다.");
                return;
            }

            var analysis = BuildVlmStyleAnalysis(detections);
            var detectionSummary = BuildDetectionSummary(detections);
            _viewModel.ApplyVlmAnalysisResult("높음", $"{analysis} 탐지 내용: {detectionSummary}");

            var alertSignature = $"{_viewModel.SelectedPrimaryTarget}:{frameId}:{BuildOverlaySignature(detections)}";
            var now = DateTimeOffset.Now;
            if (string.Equals(_lastDetectionAlertSignature, alertSignature, StringComparison.Ordinal) ||
                now - _lastMobileAlertAt < MobileAlertCooldown)
            {
                return;
            }

            _lastDetectionAlertSignature = alertSignature;
            _lastMobileAlertAt = now;
            var evidencePng = CaptureElementAsPng(CameraPanel);
            _ = _mobileAlertHubService.PublishAlertAsync(
                "운용통제 위험 알림",
                analysis,
                detectionSummary,
                _viewModel.CurrentThreatLevel,
                evidencePng);
            _viewModel.AppendImportantLog("모바일 앱으로 위험 알림을 전송했습니다.");
        }   
    }
    
