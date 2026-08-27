#import <UIKit/UIKit.h>

// 가벼운 임팩트 햅틱 — Unity Haptics.LightTick()에서 호출.
// generator를 매번 새로 만들지 않고 재사용 + prepare로 지연 최소화.
extern "C" void _Haptic_LightImpact(void)
{
    if (@available(iOS 10.0, *)) {
        static UIImpactFeedbackGenerator *gen = nil;
        static dispatch_once_t onceToken;
        dispatch_once(&onceToken, ^{
            gen = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
        });
        [gen prepare];
        [gen impactOccurred];
    }
}
