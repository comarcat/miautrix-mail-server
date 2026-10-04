import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { DesktopApp } from '../App';

describe('DesktopApp UI', () => {
    it('renders locally bundled admin console without remote resources', () => {
        render(<DesktopApp />);
        expect(screen.getByText('Miautrix Admin')).toBeInTheDocument();
        expect(screen.getByText('Local admin console.')).toBeInTheDocument();

        // Assert no external iframes or webviews point to arbitrary http targets
        const iframes = screen.queryAllByRole('presentation');
        iframes.forEach(iframe => {
            expect(iframe.getAttribute('src')).not.toMatch(/^https?:\/\//i);
        });
    });
});
