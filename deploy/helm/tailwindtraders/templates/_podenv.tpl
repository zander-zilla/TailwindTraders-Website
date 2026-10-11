{{- define "tt.envFrom" -}}
envFrom:
  - configMapRef:
      name: {{ include "tt.configName" . }}
  - secretRef:
      name: {{ include "tt.secretName" . }}
{{- end -}}
