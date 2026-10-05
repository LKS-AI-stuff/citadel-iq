import { Box, ButtonBase, Tooltip, Typography, alpha } from '@mui/material';
import { splitCitations } from '../../utils/citations';
import type { AnswerSourceDto } from '../../types/ask';

interface AnswerTextProps {
  text: string;
  sources: AnswerSourceDto[];
  onCitationClick: (number: number) => void;
  isStreaming?: boolean;
}

/** Plain-text answer (React-escaped, line breaks preserved) with valid `[n]` markers rendered as chips and invalid ones dropped. */
export function AnswerText({ text, sources, onCitationClick, isStreaming = false }: AnswerTextProps) {
  const parts = splitCitations(text, sources.length);

  return (
    <Typography variant="body2" component="div" sx={{ whiteSpace: 'pre-wrap', lineHeight: 1.7 }}>
      {parts.map((part, index) =>
        part.type === 'text' ? (
          <span key={index}>{part.value}</span>
        ) : (
          <span key={index}>
            {part.numbers.map((n) => {
              const source = sources.find((s) => s.number === n);
              return (
                <Tooltip key={n} title={source?.fileName ?? `Source ${n}`}>
                  <ButtonBase
                    onClick={() => onCitationClick(n)}
                    aria-label={`Go to source ${n}`}
                    sx={{
                      verticalAlign: 'baseline',
                      mx: 0.25,
                      px: 0.75,
                      minWidth: 20,
                      borderRadius: '6px',
                      fontSize: 11,
                      fontWeight: 700,
                      color: 'primary.main',
                      bgcolor: (t) => alpha(t.palette.primary.main, 0.14),
                      '&:hover': { bgcolor: (t) => alpha(t.palette.primary.main, 0.26) },
                    }}
                  >
                    {n}
                  </ButtonBase>
                </Tooltip>
              );
            })}
          </span>
        ),
      )}
      {isStreaming && (
        <Box
          component="span"
          aria-hidden
          sx={{
            display: 'inline-block',
            width: 7,
            height: 15,
            ml: 0.5,
            verticalAlign: 'text-bottom',
            bgcolor: 'primary.main',
            opacity: 0.7,
            animation: 'ask-caret 1s steps(2) infinite',
            '@keyframes ask-caret': { '50%': { opacity: 0 } },
          }}
        />
      )}
    </Typography>
  );
}
