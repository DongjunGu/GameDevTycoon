#import <Foundation/Foundation.h>
#import <AuthenticationServices/AuthenticationServices.h>

extern void UnitySendMessage(const char* obj, const char* method, const char* msg);

static const char* GAME_OBJECT_NAME = "AppleLogin";

// iOS 13+ Sign in with Apple
API_AVAILABLE(ios(13.0))
@interface AppleSignInDelegate : NSObject <ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding>
@end

@implementation AppleSignInDelegate

- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController*)controller API_AVAILABLE(ios(13.0)) {
    if (@available(iOS 15.0, *)) {
        for (UIScene* scene in [UIApplication sharedApplication].connectedScenes) {
            if ([scene isKindOfClass:[UIWindowScene class]]) {
                UIWindowScene* windowScene = (UIWindowScene*)scene;
                if (windowScene.activationState == UISceneActivationStateForegroundActive) {
                    return windowScene.windows.firstObject;
                }
            }
        }
    }
    return [UIApplication sharedApplication].keyWindow;
}

- (void)authorizationController:(ASAuthorizationController*)controller
   didCompleteWithAuthorization:(ASAuthorization*)authorization API_AVAILABLE(ios(13.0)) {
    if ([authorization.credential isKindOfClass:[ASAuthorizationAppleIDCredential class]]) {
        ASAuthorizationAppleIDCredential* credential = (ASAuthorizationAppleIDCredential*)authorization.credential;
        NSData* tokenData = credential.identityToken;
        NSString* token = [[NSString alloc] initWithData:tokenData encoding:NSUTF8StringEncoding];
        UnitySendMessage(GAME_OBJECT_NAME, "OnTokenReceived", [token UTF8String]);
    }
}

- (void)authorizationController:(ASAuthorizationController*)controller
          didCompleteWithError:(NSError*)error API_AVAILABLE(ios(13.0)) {
    NSString* errMsg = [NSString stringWithFormat:@"%@", error.localizedDescription];
    UnitySendMessage(GAME_OBJECT_NAME, "OnTokenFailed", [errMsg UTF8String]);
}

@end

static AppleSignInDelegate* _delegate = nil;

extern "C" {

void _RequestAppleSignIn() {
    if (@available(iOS 13.0, *)) {
        _delegate = [[AppleSignInDelegate alloc] init];

        ASAuthorizationAppleIDProvider* provider = [[ASAuthorizationAppleIDProvider alloc] init];
        ASAuthorizationAppleIDRequest* request = [provider createRequest];
        request.requestedScopes = @[ASAuthorizationScopeFullName, ASAuthorizationScopeEmail];

        ASAuthorizationController* controller =
            [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[request]];
        controller.delegate = _delegate;
        controller.presentationContextProvider = _delegate;
        [controller performRequests];
    } else {
        UnitySendMessage(GAME_OBJECT_NAME, "OnTokenFailed", "iOS 13.0 미만은 지원하지 않습니다.");
    }
}

}
