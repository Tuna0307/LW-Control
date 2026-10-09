import { useI18n } from "./i18n.jsx";

// Original Gi profile-switch branch, index-BVfnK1wp.js UTF-8 byte 373027.
// Read errors are handled by original producers/sidebar; no switch-error UI exists.
export function ProfileSwitchState({ loading = false }) {
  const { t } = useI18n();
  return loading ? <div className="panel profile-switch-loading"><span className="muted">{t("common.processing")}</span></div> : null;
}
