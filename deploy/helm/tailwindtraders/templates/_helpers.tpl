{{- define "tt.name" -}}{{ .Chart.Name }}{{- end -}}
{{- define "tt.fullname" -}}{{- if contains .Chart.Name .Release.Name -}}{{ .Release.Name | trunc 63 | trimSuffix "-" }}{{- else -}}{{ printf "%s-%s" .Release.Name .Chart.Name | trunc 63 | trimSuffix "-" }}{{- end -}}{{- end -}}
{{- define "tt.selectorLabels" -}}
app.kubernetes.io/name: {{ include "tt.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}
{{- define "tt.labels" -}}
{{ include "tt.selectorLabels" . }}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version }}
app.kubernetes.io/version: {{ .Values.image.tag | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
app.kubernetes.io/part-of: tailwindtraders
{{- end -}}
{{- define "tt.serviceAccountName" -}}{{- if .Values.serviceAccount.create -}}{{ default (include "tt.fullname" .) .Values.serviceAccount.name }}{{- else -}}{{ default "default" .Values.serviceAccount.name }}{{- end -}}{{- end -}}
{{- define "tt.configName" -}}{{ include "tt.fullname" . }}-config{{- end -}}
{{- define "tt.secretName" -}}{{- if eq .Values.secrets.mode "existing" -}}{{ required "secrets.existingSecretName is required when secrets.mode=existing" .Values.secrets.existingSecretName }}{{- else -}}{{ include "tt.fullname" . }}-secrets{{- end -}}{{- end -}}
{{- define "tt.image" -}}{{ .Values.image.repository }}:{{ .Values.image.tag }}{{- end -}}
{{/* Config + secret are hooks too (weight -10) so the pre-install migrate Job can use them. */}}
{{- define "tt.hookAnnotations" -}}
helm.sh/hook: pre-install,pre-upgrade
helm.sh/hook-weight: "-10"
helm.sh/hook-delete-policy: before-hook-creation
{{- end -}}
