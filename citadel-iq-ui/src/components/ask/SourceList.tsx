import { useState } from 'react';
import { Button, Collapse, Stack, Typography } from '@mui/material';
import ExpandMoreRoundedIcon from '@mui/icons-material/ExpandMoreRounded';
import { SearchResultCard } from '../search/SearchResultCard';
import { sourceElementId } from '../../utils/citations';
import { documentsApi } from '../../api/documentsApi';
import type { AnswerSourceDto } from '../../types/ask';

interface SourceListProps {
  turnId: number;
  sources: AnswerSourceDto[];
  cited: number[];
  query: string;
  highlighted: number | null;
}

export function SourceList({ turnId, sources, cited, query, highlighted }: SourceListProps) {
  const [showOthers, setShowOthers] = useState(false);

  const citedSources = cited.map((n) => sources.find((s) => s.number === n)).filter((s): s is AnswerSourceDto => !!s);
  const others = sources.filter((s) => !cited.includes(s.number));

  const renderCard = (source: AnswerSourceDto, index: number) => (
    <div key={source.number} id={sourceElementId(turnId, source.number)}>
      <SearchResultCard
        result={source}
        query={query}
        citationNumber={source.number}
        highlighted={highlighted === source.number}
        animationDelayMs={index * 40}
        onDownload={() => window.open(documentsApi.getDownloadUrl(source.documentId), '_blank')}
      />
    </div>
  );

  return (
    <Stack spacing={1.25}>
      {citedSources.length > 0 && (
        <>
          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, letterSpacing: '0.04em' }}>
            SOURCES
          </Typography>
          {citedSources.map(renderCard)}
        </>
      )}

      {others.length > 0 && (
        <>
          <Button
            size="small"
            onClick={() => setShowOthers((v) => !v)}
            aria-expanded={showOthers}
            endIcon={
              <ExpandMoreRoundedIcon
                sx={{ transform: showOthers ? 'rotate(180deg)' : 'none', transition: 'transform 0.2s ease' }}
              />
            }
            sx={{ alignSelf: 'flex-start', textTransform: 'none', fontWeight: 600 }}
          >
            {citedSources.length > 0 ? 'Other passages considered' : 'Passages considered'} ({others.length})
          </Button>
          <Collapse in={showOthers} unmountOnExit>
            <Stack spacing={1.25}>{others.map(renderCard)}</Stack>
          </Collapse>
        </>
      )}
    </Stack>
  );
}
